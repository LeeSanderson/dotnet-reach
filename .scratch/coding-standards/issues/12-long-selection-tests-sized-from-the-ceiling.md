# 12: The long-selection tests are sized from the ceiling

**What to build:** The tests for chunking and response-file delivery exercise those paths on every OS, by building a selection that overflows whatever command-line ceiling the current platform has. Today they overflow only the Windows ceiling (8,000), not the Linux one (100,000). See [the spec](../spec.md) and ADR-0009.

- **Test-only change.** Size the long-selection inputs in the rendering and invocation tests from the platform's command-line ceiling constant (e.g. ceiling divided by a name's length, plus a margin).
- The ceiling stays a fixed per-platform constant. Do not make it injectable or configurable (ADR-0009: never a flag).
- ADR-0009 already has a paragraph explaining the Linux ceiling. Leave it as it is.

**Blocked by:** 09 (integrate and verify the coding standard)

**Status:** ready-for-agent

- [ ] The rendering test "the chunker partitions the set with nothing dropped or duplicated" passes on Linux
- [ ] The invocation tests "a long selection on the testing platform goes through a response file", "re-running overwrites a side-car rather than accumulating" and "a long selection on a host with no channel is chunked" pass on Linux
- [ ] All four still pass on Windows
- [ ] No production code changes
- [ ] Build and format gates remain clean
