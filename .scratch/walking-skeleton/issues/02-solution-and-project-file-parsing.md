# Solution and project-file parsing without MSBuild

Type: research
Status: resolved
Blocked by: (none)

## Question

Reach needs the project list and the project graph without taking an MSBuild dependency —
PRD §9.2 rejected loading solutions through MSBuild for being slow, and assembly discovery
must work under `--no-build` where there is no build to interrogate.

**Solution files.** Confirm whether `Microsoft.VisualStudio.SolutionPersistence` is the
right library for reading both `.sln` and `.slnx`, what it exposes (project paths, project
type, solution configurations), what it depends on, and whether it is a supported public
package or an implementation detail of the SDK. If it is unsuitable, what is the
alternative — `dotnet sln list`, or parsing both formats directly?

**Project files.** How reliably can `ProjectReference` elements be read from raw XML to
build the project graph? Specifically:

- `ReferenceOutputAssembly=false` references, which matter because the output is copied
  but no metadata reference exists (see ADR-0002).
- `OutputItemType=Analyzer` references, which identify source generator and analyser
  projects — the mandatory rule-table row depends on recognising these.
- References whose path or condition depends on MSBuild properties, and how often that
  actually occurs in practice.
- Where `AssemblyName` may be overridden, including from `Directory.Build.props`.

**Output locations.** What determines where build output lands, without evaluating
MSBuild: default `bin/{Configuration}/{TFM}/`, `UseArtifactsOutput` and its
`artifacts/bin/{project}/{configuration}/` layout, and `OutputPath` overrides. What can
Reach detect versus what must be a command-line input?

**Debug symbols.** Where portable PDBs land relative to the assembly, and when they are
embedded in the assembly instead of sitting beside it.

Findings go to `.scratch/walking-skeleton/research/`. Do **not** attempt to verify the
PDB source-checksum claim from ADR-0003 here; that is deliberately deferred to the
implementation ticket.

## Answer

Full findings: [research/02-solution-and-project-file-parsing.md](../research/02-solution-and-project-file-parsing.md).

**Solution files: `Microsoft.VisualStudio.SolutionPersistence` 1.0.52**, behind a port,
plus a small `System.Text.Json` reader for `.slnf` solution filters. MIT, zero NuGet
dependencies on `net8.0`, and no MSBuild reference at all — verified by reflecting over the
shipped assembly. It is what `dotnet sln`, `Microsoft.Build.dll` and NuGet.Client all use,
so Reach's reading of a solution agrees with the build's by construction. One API reads
both `.sln` and `.slnx`, which matters because `.slnx` is GA as of SDK 9.0.200 and is the
**default** for `dotnet new sln` on .NET 10 — a `.sln`-only design would be obsolete on
arrival.

`dotnet sln list` was rejected: no JSON output, a localised header, paths only (no project
type, no per-configuration build flag), and it fails outright when a directory contains
both `Foo.sln` and `Foo.slnx` — which is exactly what `dotnet sln migrate` leaves behind.

Caveats to encode: `OpenAsync` is async-only; `Type` is empty for a plain `.csproj`, so use
`Extension`, and note the type GUID differs between `.sln` and `.slnx` for the same
project; paths are neither canonicalised nor separator-normalised for `.sln`; there is no
`netstandard2.0` asset. It never opens the referenced project files, which is exactly what
`--no-build` needs.

**`ReferenceOutputAssembly=false` produces neither a metadata reference nor a copy of the
assembly.** `Private=false` is the separate switch governing content copying. ADR-0002 has
been corrected — the conclusion stands, but for the other reason: the compiler omits
metadata references to assemblies whose types are never named, so the metadata closure is
narrower than the real one and project references remain the right source.

**Analyser and generator projects are harder to detect than assumed.**
`OutputItemType=Analyzer` is conventional, not prescribed — the Roslyn cookbooks contain
zero occurrences of it, and generators also arrive via `PackageReference` or bare
`<Analyzer>` items with no `ProjectReference` at all. A generator edge is a build-order and
compilation-input edge rather than a runtime one, so it probably warrants its own edge
provenance. Escalated to
[The unmappable-change rule table](15-the-unmappable-change-rule-table.md).

**Output layout is ambiguous on disk.** The *same* `OutputPath` value produces different
layouts depending on where it was set: from the project file it still gets TFM and RID
appended; as an MSBuild global property — which is what `dotnet build -o` forwards — it
does not, because global properties cannot be reassigned during evaluation. Nothing on disk
records which happened. Artifacts output is a third layout, and a single-targeted project
gets no TFM segment there. `--artifacts-path` must be cascaded to `--no-build`. The
conclusion is to predict candidate locations, verify by directory enumeration, and error
when an expected assembly is absent. Escalated to
[Assembly discovery under ambiguous output layouts](14-assembly-discovery-under-ambiguous-output-layouts.md).

**Debug symbols** land at `$(OutDir)$(TargetName).pdb`. `DebugType` defaults to `portable`
in Release as well as Debug, so ADR-0003's verification does not force anyone into a Debug
build — but `$(DebugSymbols)` is a false signal, evaluating `false` in Release while a PDB
is still produced. Read `DebugType`. A PDB beside an assembly proves nothing about
provenance, since `.pdb` is an `AllowedReferenceRelatedFileExtension` and dependencies'
symbols are copied into the consuming project's output too — so first-party classification
must resolve document paths, never check for a file's existence.

### Not established

How often property-parameterised or conditioned `ProjectReference` paths occur in the wild:
no first-party source states it, and the agent recorded a small clustered local scan (470
project files, 798 references, none parameterised or conditioned) explicitly labelled as
orientation rather than evidence. Also unresolved: whether `.slnx` ever sat behind a Visual
Studio preview flag; why `-o` on a multi-targeted project emits no warning; and the real
cost of `dotnet msbuild -getProperty` on a solution. Two findings are negative results from
exhaustive grep rather than documented statements, and are flagged as such in the research
file.
