# Tool target framework versus the Roslyn dependency

Type: grilling
Status: open
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
