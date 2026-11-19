# 10: The summary line is culture-invariant

**What to build:** An adopter on any locale sees the same human summary line, e.g. `selected 1,234 of 5,678 tests (50%)`, whether the host is en-US, de-DE, or a container with a `C` locale. See [the spec](../spec.md) and ADR-0016.

- The measurement summary line formats its counts with the invariant culture.
- The percentage is written as a rounded whole number followed by a literal `%`, **not** with the `P0` specifier, because even the invariant culture's percent pattern inserts a space.
- The tests are correct as written and stay unchanged. The production code was wrong.

**Blocked by:** 09 (integrate and verify the coding standard)

**Status:** ready-for-agent

- [x] The measurement test for "the summary line carries both numbers" passes on Linux, unchanged
- [x] The human-summary test for "the summary never lists the selected tests" passes on Linux, unchanged
- [x] The summary line is identical under the en-US, de-DE and invariant cultures
- [x] Build and format gates remain clean

## Comments

- `ReportMeasurement.Describe` now builds the line with `string.Create(CultureInfo.InvariantCulture, ...)`; the percentage is `Math.Round(ratio * 100, MidpointRounding.ToEven)` (ADR-0016's default rounding, and what `P0` gives on .NET 10 for an exact midpoint: 1 of 8 is `12%`) followed by a literal `%`. (Corrected in the code-review fixes; this first said `AwayFromZero`, which was wrong.)
- New theory `MeasurementTests.The_summary_line_is_the_same_under_every_culture` sets `CurrentCulture` to en-US, de-DE and invariant and expects `selected 1,234 of 2,468 tests (50%) · ...` each time.
- Verified in `mcr.microsoft.com/dotnet/sdk:10.0` against a clone of the branch: `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean, and the two named tests plus the new theory pass with the process locale set to `C`, `en_US.UTF-8` and `de_DE.UTF-8`. Full suite passes on Windows (527/527).
