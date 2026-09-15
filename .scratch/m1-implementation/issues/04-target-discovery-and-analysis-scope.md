# Target discovery, solution parsing and analysis scope

Status: resolved
Depends on: 01
Spec: [§4.2](../../walking-skeleton/spec.md#42-target-discovery), [§6.1](../../walking-skeleton/spec.md#61-analysis-scope) · [ADR-0002](../../../docs/adr/0002-analysis-scope-is-the-union-of-test-project-closures.md), [ADR-0011](../../../docs/adr/0011-a-solution-wins-over-a-project-in-target-discovery.md)

## Goal

From a directory or a path, produce the target, the **project graph**, the test projects, the
**analysis scope**, and the **expected assembly-instance set** everything downstream depends
on — without MSBuild.

## Scope

**Discovery**, current directory **only**, never recursive. Recursion is where "confidently
wrong" lives: a monorepo with six solutions gets a coin flip, and the wrong solution silently
produces a wrong analysis scope.

| Found | Result |
|---|---|
| exactly one `.sln`/`.slnx` | **that solution wins, whatever projects sit beside it** |
| more than one solution | exit 1, naming what was found |
| no solution, exactly one project | that project |
| no solution, more than one project | exit 1, naming what was found |
| a `.slnf` solution filter | exit 1, naming the underlying `.sln` |

**This deliberately diverges from MSBuild**, which compares base names and errors when they
differ. Do not "fix" it to match. For Reach a solution and a project are two different
**analysis scopes**, so choosing the project is choosing the *narrower* one — a silent
under-selection with a plausible-looking report. A `.slnf` is rejected rather than expanded,
because a caller who passes a filter has stated an intent Reach would otherwise ignore without
saying so.

Two further cases:

- **A `.csproj` declaring no tests** — the union of test-project closures is empty. This is a
  mis-invocation, not `nothing-selected`; reporting "no tests affected" would be a lie a
  pipeline believes. **Exit 1**, naming the project.
- **A `.csproj` outside every solution** — legal, since the solution only ever supplied the
  expected project list. It costs the check that turns a missing assembly into an error, so
  emit a `scope` notice and continue.

**Solution parsing** uses `Microsoft.VisualStudio.SolutionPersistence` — MIT, zero NuGet
dependencies, no MSBuild reference, and what `dotnet sln`, `Microsoft.Build.dll` and
NuGet.Client all use, so Reach's reading agrees with the build's by construction. One API reads
both `.sln` and `.slnx`, which matters because `.slnx` is the default for `dotnet new sln` on
.NET 10. `dotnet sln list` was rejected: no JSON output, a localised header, paths only, and it
fails outright when a directory holds both `Foo.sln` and `Foo.slnx` — exactly what
`dotnet sln migrate` leaves behind.

Caveats to encode: `OpenAsync` is async-only; `Type` is empty for a plain `.csproj`, so switch
on `Extension`; the type GUID differs between `.sln` and `.slnx` for the same project; `.sln`
paths are neither canonicalised nor separator-normalised. It never opens the referenced project
files, which is exactly what `--no-build` needs. `.slnf` is read with a small
`System.Text.Json` reader, only far enough to name the underlying `.sln` in the exit-1 message.

**The project graph** comes from `ProjectReference` elements read from raw project XML, not
from assembly references in metadata: the compiler omits references to assemblies whose types
are never named, so the metadata closure is narrower than the real one.
`ReferenceOutputAssembly=false` produces neither a metadata reference nor a copy — `Private=false`
is the separate switch governing copying — and such a reference expresses build order rather
than runtime executability, so including it in the closure errs wide, which is the safe
direction. Read `AssemblyName` overrides, including from `Directory.Build.props`.

**Analysis scope is the union of the transitive project closures of the solution's test
projects.** Code outside every test closure cannot execute in any test process. Test projects
are identified by their referenced test-framework packages — see ticket 11's recognition table,
which this ticket may stub with a minimal version and ticket 11 replaces.

**Changed-side narrowing is forbidden and is the most attractive available mistake.** Do not
restrict analysis scope to the projects that transitively depend on the changed project: a
caller usually references the interface, not the implementation, so the caller's project is not
a dependent of the implementation's project and would be excluded despite being affected. Build
scope may be narrowed; changed-side analysis scope may not.

**The expected assembly-instance set** is every `(project, target framework)` pair in the
analysis scope, **excluding** projects that produce no output assembly: generator and analyser
projects, and projects referenced with `ReferenceOutputAssembly=false`. Ticket 07 turns a
missing member of this set into exit 3, so a wrong exclusion here is a false error there.

## Acceptance criteria

- A test per discovery row, including one solution beside three projects (solution wins) and a
  `.slnf` (exit 1 naming the `.sln`).
- Both `.sln` and `.slnx` parse to the same project list for the same solution.
- A solution containing a project with `ReferenceOutputAssembly=false` still has that project
  in its closure.
- A `.csproj` with no test project in its closure exits 1 naming the project.
- A `.csproj` outside every solution emits the `scope` notice and continues.
- Project-graph closure is transitive and handles a diamond without duplicating.
- **A negative test asserting scope is not narrowed to dependents of a changed project**, using
  the interface-in-a-third-project shape from ADR-0002.
- A multi-targeted project contributes several assembly instances to the expected set.

## Out of scope

Finding the assemblies on disk — ticket 07. Property-parameterised or conditioned
`ProjectReference` paths: no evidence exists about how often they occur, so read them literally
and let ticket 07's scan-and-verify absorb the consequence.

## Comments

**Implemented** in `Reach.Core/Projects`: `TargetDiscovery`, `SolutionReader`,
`ProjectFileReader`/`ProjectFile`, `ProjectGraph`, `TestProjectRecognition` (the stub ticket 11
replaces), `AnalysisScope`/`AnalysisScopeResolver`. `Microsoft.VisualStudio.SolutionPersistence`
1.0.52 is now a `Reach.Core` package reference.

**One reading of the discovery table had to be settled.** The table's `.slnf` row is about the
*chosen* target, not about any `.slnf` file in the directory: a `.sln` or `.slnx` present still
wins, and only a filter reached as the target — passed explicitly, or alone in the directory —
exits 1. A convenience filter sitting beside the solution it filters is a common layout, and
refusing it would break repositories for no gain. Both halves have a named test.

**`Paths`** (`Reach.Core/Paths.cs`) is new and is where every path comparison goes:
case-insensitive on Windows and macOS, case-sensitive elsewhere, plus `IsUnder` for the rule
table's directory containment — where a prefix test alone would let `/src/Foo` claim
`/src/FooBar`. Ticket 01 named path comparison as the Windows/Linux fault line; this is the one
place to get it right.

**The expected assembly-instance set excludes a project when every reference reaching it
carries `ReferenceOutputAssembly=false` or `OutputItemType="Analyzer"`, unless it is itself a
test project.** The exclusion is driven by how a project is *referenced* rather than by
anything in the project itself, because that is what decides whether its output lands where
Reach scans. A generator project still appears in the closure — such a reference expresses
build order rather than runtime executability, so including it errs wide.

**`ProjectFileReader` is raw XML and evaluates nothing** — no conditions, no property
expansion, no imports. It reads the nearest `Directory.Build.props` walking up, which is what
MSBuild does, and takes the last unconditioned declaration of a property. A value containing
`$(` is discarded rather than guessed at: `AssemblyName` falls back to the file's base name,
and a framework or reference path is dropped. Ticket 07's scan-and-verify absorbs the
consequence, which is an error rather than a silent under-selection.

**`AnalysisScopeResolver` takes no changed set and must not grow one.** The forbidden narrowing
has a named test built on ADR-0002's shape — `Caller` references `Contracts`, never
`Implementation`, so narrowing to dependents drops `Caller` despite a change in
`Implementation` reaching it.

**The "outside every solution" check walks up** from the project's directory looking for a
solution that lists it, and emits `project-outside-every-solution` (`scope`) when none does.
That is a fourth notice code.

One fixture detail worth keeping: a `.slnf` written with single backslashes is not valid JSON,
so `ProjectTree.AddSlnf` doubles them. The first version of the fixture produced a file no
parser accepts and the exit-1 message silently lost the solution name.
