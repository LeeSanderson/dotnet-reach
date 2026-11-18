# 10: The summary line is culture-invariant

**What to build:** An adopter on any locale sees the same human summary line, e.g. `selected 1,234 of 5,678 tests (50%)`, whether the host is en-US, de-DE, or a container with a `C` locale. See [the spec](../spec.md) and ADR-0016.

- The measurement summary line formats its counts with the invariant culture.
- The percentage is written as a rounded whole number followed by a literal `%`, **not** with the `P0` specifier, because even the invariant culture's percent pattern inserts a space.
- The tests are correct as written and stay unchanged. The production code was wrong.

**Blocked by:** 09 (integrate and verify the coding standard)

**Status:** ready-for-agent

- [ ] The measurement test for "the summary line carries both numbers" passes on Linux, unchanged
- [ ] The human-summary test for "the summary never lists the selected tests" passes on Linux, unchanged
- [ ] The summary line is identical under the en-US, de-DE and invariant cultures
- [ ] Build and format gates remain clean
