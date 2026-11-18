# Reach's output is culture-invariant

Every number, percentage and date Reach writes — the summary line, the report, notices — is
formatted with `CultureInfo.InvariantCulture`, so the same run prints the same text on every
machine. The summary line is `selected 1,234 of 5,678 tests (50%)` whether the host is en-US,
de-DE, or a container with a `C` locale (which .NET maps to the invariant culture, whose `P0`
renders `50 %` — the bug that surfaced this).

Reach's output is pasted into pull requests, grepped in CI logs and compared between runs; a
line that changes shape with the host's locale breaks all three, and a CI image's locale is
rarely something the adopter chose. Following the user's locale was the alternative and was
rejected for that reason.

## Consequences

The coding-standards analyzers (CA1305, MA0076) force a culture to be *named* on every
formatting call, but accept `CurrentCulture` as readily as `InvariantCulture`. This ADR is the
rule for which one to name. Percentages are written by hand (`{Math.Round(ratio * 100):0}%`)
rather than with `P0`, because even the invariant culture's percent pattern carries a space.
