# The tool targets net10.0

`dotnet-reach` targets `net10.0`, a single target framework, with `RollForward: LatestMajor`
and the latest stable `Microsoft.CodeAnalysis.CSharp`. It therefore requires a .NET 10 or
later runtime on the machine it runs on.

A reader will reasonably ask why a tool that reads assembly metadata and parses C# — work
the BCL has supported for a decade — requires the newest LTS runtime, when its stated
audience is a consultant introducing it to an unfamiliar repository in an afternoon. This is
why, and what it costs.

## The cost, stated plainly

Roll-forward is one-directional. A `net10.0` tool runs on .NET 10 and above and cannot run
below, so on a build agent carrying only the .NET 8 SDK — which is what a solution pinning an
8.x `global.json` installs — Reach does not run until a runtime is added. That agent belongs
to exactly the codebase Reach is for: large, established, slow-testing, and in no hurry to
move target framework.

`RollForward: LatestMajor` is kept for the same reason it was attractive at `net8.0`, and it
is easy to lose in this decision rather than because of it: the default policy is `Minor` and
will not cross a major version, so without it a `net10.0` tool fails on a machine carrying
only .NET 11.

## Considered options

**`net8.0` with Roslyn's `netstandard2.0` asset** was the recommendation, and it was viable
on the evidence rather than a compromise. `Microsoft.CodeAnalysis.CSharp` 5.9.0 ships only
`net10.0` and `netstandard2.0`; the `netstandard2.0` asset is the same compiler front-end
retargeted from one source set — zero TFM `#if` in `Parser/` or `Syntax/`, identical public
surface, and byte-identical parse output against a C# 1–14 corpus on .NET 8, 9 and 10. It
would have kept the floor at .NET 8 and the parser current, for about 1.4 MB, two app-local
assemblies, and exposure to `NU1605` if a future dependency pinned `System.Collections.Immutable`
lower. The owner chose the narrower floor over that closure. Because the asset is
fully equivalent, **lowering the floor later is a csproj edit rather than a redesign** — the
option is deferred, not destroyed.

**Pinning Roslyn to keep `net8.0` native** is not available: 5.6.0 is the last stable version
with a `net8.0` asset and 5.3.0 the last with `net9.0`, so this meant a compiler two releases
behind, permanently, on someone else's schedule. Reach's Roslyn version bounds the C# it can
read, and it fails worst on the newest codebases — whose owners are most likely to adopt a new
tool.

**Multi-targeting `net8.0;net10.0`** buys the widest floor and costs a conditional-compilation
matrix in a tool whose value proposition is correctness. Rejected without much argument.

## Consequences

The dependency graph is trivial: `System.CommandLine` 2.0.x and `Microsoft.CodeAnalysis.CSharp`
5.9.0, both resolving native `net10.0` assets with no facade chain and no version-conflict
class to manage. Keeping it that way is worth something, since the alternative floor's main
cost was transitive.

This sits in tension with **PRD §9.1's "no infrastructure ask"** and §1.2's secondary audience.
It is not a contradiction — §9.1's target is a standing service (coverage storage, an
instrumented main-branch build, a cache-miss policy), and a one-time runtime install is not
one — and the PRD names no target framework, so no amendment is owed. But the tension is real
and belongs on the record rather than in a reader's head.

**Revisit trigger:** adoption friction traceable to the runtime requirement. That is the
evidence the recommendation lacked, and it is the only thing that should reopen this — not the
argument, which has been had.
