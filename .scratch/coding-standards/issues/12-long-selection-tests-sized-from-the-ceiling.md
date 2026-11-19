# 12: The long-selection tests are sized from the ceiling

**What to build:** The tests for chunking and response-file delivery exercise those paths on every OS, by building a selection that overflows whatever command-line ceiling the current platform has. Today they overflow only the Windows ceiling (8,000), not the Linux one (100,000). See [the spec](../spec.md) and ADR-0009.

- **Test-only change.** Size the long-selection inputs in the rendering and invocation tests from the platform's command-line ceiling constant (e.g. ceiling divided by a name's length, plus a margin).
- The ceiling stays a fixed per-platform constant. Do not make it injectable or configurable (ADR-0009: never a flag).
- ADR-0009 already has a paragraph explaining the Linux ceiling. Leave it as it is.

**Blocked by:** 09 (integrate and verify the coding standard)

**Status:** ready-for-agent

- [x] The rendering test "the chunker partitions the set with nothing dropped or duplicated" passes on Linux
- [x] The invocation tests "a long selection on the testing platform goes through a response file", "re-running overwrites a side-car rather than accumulating" and "a long selection on a host with no channel is chunked" pass on Linux
- [x] All four still pass on Windows
- [x] No production code changes
- [x] Build and format gates remain clean

## Comments

- A shared test helper `tests/Reach.Tests/Rendering/LongSelection.cs` sizes the selection as `CommandLineCeiling.Characters / name length + 100`, so the bare names alone overflow the ceiling. `Every_chunk_fits_under_the_ceiling` uses it too, so that test is meaningful on Linux. Verified on Linux in `mcr.microsoft.com/dotnet/sdk:10.0` (build with `-warnaserror` clean; each named test passes) and on Windows (full suite: 524 passed).
