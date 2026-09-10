# Tool target framework versus the Roslyn dependency

Type: grilling
Status: resolved
Blocked by: (none)

## Question

Charting decided the tool targets `net8.0` with `RollForward: LatestMajor`, so it installs
on any modern agent — PRD §1.2's "introduced in an afternoon, on a repository they have
never seen". [Tool packaging and CLI library](03-tool-packaging-and-cli-library.md)
verified that mechanism works, and surfaced a conflict with it:
`Microsoft.CodeAnalysis.CSharp` dropped its `net8.0` asset at 5.9.0, with 5.6.0 the last
stable version carrying one.

The packaging consequence is mild — a `net8.0` tool resolves Roslyn's `netstandard2.0`
asset and a larger dependency group, and `PackAsTool` inlines it all anyway. The real
question is underneath it.

**Reach's Roslyn version bounds the C# it can read.** Change detection parses the
customer's source. A tool pinned to an old Roslyn cannot correctly parse language features
newer than that Roslyn — and it fails on the *newest* codebases, which are the ones whose
owners are most likely to adopt a new tool. That reframes this from a packaging preference
into a product constraint, and it interacts with the correctness rule: a parse that fails
must widen the selection, never quietly produce an empty changed set.

**Options:**

- **Pin Roslyn 5.6.0, keep `net8.0`.** Preserves install-anywhere. Freezes the C# Reach can
  parse, permanently, on a schedule set by someone else.
- **Multi-target `net8.0;net10.0`.** Broadest reach, latest Roslyn where available. Costs a
  conditional-compilation matrix in a tool whose value proposition is correctness — the
  thing charting rejected multi-targeting for in the first place.
- **Target `net10.0`.** Latest Roslyn, single code path, and it abandons the
  install-anywhere property that motivated `net8.0`. How many target agents actually lack
  .NET 10 by the time Reach ships?
- **Keep `net8.0` and accept the `netstandard2.0` Roslyn asset.** Latest Roslyn, bigger
  package. Establish whether that asset is functionally equivalent for parsing, or merely
  present.

**To settle as part of this:** what Reach does when it meets C# it cannot parse. That is a
correctness rule, and it belongs in the spec whichever target framework wins.

Resolving this revises decision 16 on the map.

## Answer

**The tool targets `net10.0`, single TFM, with the latest stable Roslyn** —
[ADR-0013](../../../docs/adr/0013-the-tool-targets-net10-0.md). Owner decision, taken
against this ticket's recommendation, which was `net8.0` plus Roslyn's `netstandard2.0`
asset. Both options were viable on the evidence; the owner chose the narrower floor.

`RollForward: LatestMajor` **stays**, and this is the part easiest to lose: a `net10.0`
application does *not* run on a machine carrying only .NET 11, because the default
`RollForward` policy is `Minor` and will not cross a major version. The property that made
`net8.0` attractive still applies upward from `net10.0`, and it is one line of csproj.

### The Roslyn conflict dissolves rather than being traded away

The packaging conflict this ticket exists to resolve is gone: `Microsoft.CodeAnalysis.CSharp`
5.9.0's `lib/` contains exactly `net10.0` and `netstandard2.0`, so a `net10.0` tool resolves
the native asset. No app-local `System.Collections.Immutable`, no `NU1605` conflict class, no
`netstandard2.0` facade chain. The option chosen is the one where the question does not arise.

Established by fact-find, and worth keeping because it disposes of the option this ticket
listed as needing investigation — **"keep `net8.0` and accept the `netstandard2.0` asset" was
never the risk it looked like.** That asset is the same front-end retargeted from one source
set: zero TFM `#if` in `Parser/` or `Syntax/`, an identical public surface (7816 members, no
differences), and byte-identical parse fingerprints against a C# 1–14 corpus on .NET 8, 9 and
10. Roslyn's own analyzer guidance (RS1041) recommends it. So if the floor is ever lowered
again, it is a csproj edit and not a redesign — the fallback is intact, not burned.

The pinned-Roslyn option is dead either way: **5.6.0 is the last stable version with a
`net8.0` asset**, and 5.3.0 the last with `net9.0`, so "pin Roslyn, keep `net8.0`" meant
pinning a compiler two releases back. Multi-targeting was never in contention.

### What Reach does when it meets C# it cannot parse

This half turned out to matter more than the TFM, and the naive rule — *any parse error
widens* — is not a safety net. Roslyn checks language versions at **binding**, not parsing
([Parser.md](https://github.com/dotnet/roslyn/blob/main/docs/compilers/Design/Parser.md)),
and Reach only ever calls `ParseText`. Measured directly, an older parser meeting newer C#
never throws and never loses round-trip fidelity, but it fails three distinct ways:

| Outcome | Measured case | Reach sees it? |
| --- | --- | --- |
| Error diagnostic + local structural damage | C# 14 extension block on Roslyn 4.8 → `CS1513`/`CS1022`, skipped-tokens trivia, block mis-shaped as a constructor named `extension` | yes |
| Error diagnostic, structure intact | C# 13 `\e` escape → `CS1009` | yes |
| **Clean parse, structurally wrong, zero diagnostics** | `record Person(string First, string Last)` on Roslyn 3.4 → a *method* named `Person` returning a type called `record`. Also `params ReadOnlySpan`, `c?.F = 1`, partial constructors | **no** |

The third row is deliberate rather than a bug: contextual keywords are recognised only when
the feature is enabled. **A clean parse never proves a correct parse.** The rules:

1. **Parse with `LanguageVersion.Preview`, never `Latest`.** On 5.9.0 that is the difference
   between reading a `union` and silently mistaking it for a property. This is the highest-value
   line in the ticket and it is one argument.
2. **An error diagnostic *or* `SkippedTokensTrivia` in a changed file triggers whole-assembly
   widening for its project, plus a notice.** Skipped-tokens trivia earns its place because
   row 1 produced it, and it is a structural signal rather than a diagnostic one.
3. **A project whose declared `LangVersion` exceeds Reach's parser ceiling gets a notice, not
   widening.** With current Roslyn pinned the window is narrow, and widening on it would fire
   across whole modern codebases for a hazard that usually is not present.
4. **The silent misparse is a register entry** — direction *under-selection*, partially
   detectable (rows 1 and 2 only), upgrade path *ship current Roslyn and exercise the parser
   against the newest SDK in CI*.

The failure is narrower than it first looks, and the reason is worth recording so nobody
re-widens it: the **same parser reads both revisions**, so a deterministic misparse still
diffs stably, and a phantom member simply fails the join and falls through to whole-assembly
widening — which over-selects. The genuine hole is a construct whose members are *swallowed*
from the tree, as in the extension-block row, because a changed method inside one never
enters the changed set at all.

### The PRD tension, deliberately not amended

`net10.0` sits in tension with **PRD §9.1's "no infrastructure ask"** and §1.2's secondary
audience — a consultant introducing Reach on an unfamiliar repository in an afternoon. An
agent building a `net8.0` solution under a pinned 8.x `global.json` may carry no .NET 10 at
all, and roll-forward cannot go downward, so on that agent Reach does not run until a runtime
is installed.

Not routed to [PRD amendments](06-prd-amendments.md), because §9.1's "infrastructure ask"
means a standing service — coverage storage, an instrumented main-branch build, a cache-miss
policy — and a one-time runtime install is not one. The PRD names no target framework, so
nothing in it is contradicted. Recorded here and in ADR-0013 as an accepted adoption cost; if
it shows up as real adoption friction, §9.1 is where the argument reopens.

### Consequences for other tickets

- **[Project layout and ports](10-project-layout-and-ports.md)**: the dependency list is
  `System.CommandLine` 2.0.x and `Microsoft.CodeAnalysis.CSharp` 5.9.0, both native `net10.0`
  assets with no transitive closure worth managing. The `NU1605` discipline that a `net8.0`
  floor would have required does not apply.
- **[The limitations register](19-the-limitations-register.md)**: one entry, the silent
  misparse, per rule 4 above.
- **[Write the spec](12-write-the-spec.md)**: rules 1–4 are correctness rules and belong in
  the spec, not only here.
- Ticket 03's install guidance (`dnx`-led, `PackAsTool`) is unaffected. Its verification of
  `RollForward: LatestMajor` against a `net8.0` sample tool still holds as a mechanism; only
  the floor moves.
