# Tool packaging and CLI library

Type: research
Status: resolved
Blocked by: (none)

## Question

Reach ships as `dotnet-reach`, invoked as `dotnet reach` (PRD appendix), targeting
`net8.0` with `RollForward: LatestMajor` so it installs on any modern agent.

**Packaging.** What `PackAsTool` actually requires; how `RollForward` is configured for a
tool and whether it genuinely lets a `net8.0` tool run on a machine with only .NET 10 or
later installed; the difference between a global tool, a local tool manifest, and a tool
installed from a path, and which of those a CI pipeline should prefer for the "installable
in an afternoon" property PRD §1.2 requires.

**CLI library.** Which of `System.CommandLine`, `Spectre.Console.Cli` or a hand-rolled
parser to use. The deciding factor is stability rather than features: establish
`System.CommandLine`'s current release status and whether its public API has settled,
since API churn in a tool whose value proposition is trustworthiness is a real cost. Note
each option's dependency footprint — a tool that drags in a large graph is harder to
justify installing.

**Also worth knowing:** whether anything about a tool package constrains the target
framework of the assemblies it can *read*. It should not — metadata reading is version
agnostic — but confirm rather than assume.

Findings go to `.scratch/walking-skeleton/research/`, with a clear recommendation and the
reasoning behind it.

## Answer

Full findings: [research/03-tool-packaging-and-cli-library.md](../research/03-tool-packaging-and-cli-library.md).

**CLI library: `System.CommandLine` 2.0.x.** It shipped stable on 2025-11-11 alongside
.NET 10 and has had twelve monthly servicing patches since. It is a shipping product
component rather than a side project — the installed SDK's own `dotnet` CLI is built on
2.0.11, and the package repository is the .NET VMR. The parallel 3.0.0 preview line is the
.NET 11 train, not churn: its milestone holds a `Uri` converter, a resource string, a help
nit and fixes to the repo's own API-approval tooling. On `net8.0` it has **zero package
dependencies** and one 151 KB assembly. Spectre.Console.Cli remains at 0.55.0, has never
shipped 1.0, and brings three assemblies and roughly 1.5 MB.

**`RollForward: LatestMajor` works, verified rather than inferred.** Host traces confirm
the semantics — `Minor` and `LatestPatch` fail outright on an absent major, `Major` picks
the lowest higher major, `LatestMajor` the highest. End to end: `dotnetsay` 3.0.3,
Microsoft's own `net8.0`-only sample tool shipped with `"rollForward": "LatestMajor"`,
installs under the .NET 10 SDK and runs on Microsoft.NETCore.App 10.0.11. The mechanism is
the `RollForward` property flowing into the runtimeconfig that `PackTool.targets` packs
into `tools/<tfm>/any/`.

**`PackAsTool` constraints** are only three: `.NETCoreApp`, version ≥ 2.1, no
platform-specific TFM. `net8.0` passes. It forces `SuppressDependenciesWhenPacking`, so a
tool package declares no NuGet dependencies and inlines everything — which makes dependency
footprint a package-size question, not a resolution one.

**Install guidance:** lead with `dnx dotnet-reach@<version>` (`dotnet tool exec`), which
the docs say is often the better fit for CI, with a committed local tool manifest as the
pinned alternative. Do not lead with `-g`.

**Metadata reading is unconstrained by the tool's own target framework**, as assumed — and
rolling forward means Reach compiles against 8.0 APIs while running the .NET 10
implementation of `System.Reflection.Metadata`.

### Surfaced a conflict, not covered by this ticket

`Microsoft.CodeAnalysis.CSharp` dropped its `net8.0` asset at 5.9.0 (2026-08-17); the last
stable version carrying one is 5.6.0, which is what the .NET 10 SDK ships. Since change
detection is Roslyn-based, a `net8.0` tool on a newer Roslyn falls back to the
`netstandard2.0` asset and its larger dependency group. This puts the `net8.0` decision in
tension with the Roslyn dependency and is escalated to
[Tool target framework versus the Roslyn dependency](13-tool-target-framework-versus-roslyn.md).

### Not established

No *written* post-GA API-compatibility commitment for `System.CommandLine` 2.0 was found —
the stability conclusion rests on observed shipping behaviour rather than a promise. The
roll-forward path was proven by construction from host-resolution traces on a machine that
has .NET 8, 9 and 10; a machine with *only* a newer major was not observed. The SDK
installer source was not read to prove there is no install-time runtime check.

The Roslyn version dates and asset claims come from the research agent's reading of NuGet
and should be re-confirmed when versions are actually pinned.
