# Target discovery, solution parsing and analysis scope

Status: ready-for-agent
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
