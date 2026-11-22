# Report-writer and unit-level tests

Status: ready-for-agent
Depends on: —
Spec: [§10.4](../../acceptance-layer/spec.md#104-tests-in-other-suites), [§9.2](../../acceptance-layer/spec.md#92-claim-to-suite)

## Goal

The report-shape and detection claims no test checks, proven in memory.

## Scope

| Claim | Where |
|---|---|
| `runnerHost` is `dotnet-test`, absent on `skip`, present on run-all | report-writer tests (`Reporting/ReportTests.cs` or beside it) |
| `delivery` is `inline` and `run-settings` where it should be, and **absent** on an entry with no command (including the empty entries of an underivable moniker) | `Rendering/InvocationTests.cs` or report-writer tests |
| `willRun` is `0` on an entry whose total is `"unknown"` | report-writer tests |
| `ratio` is rounded to four places | `Reporting/MeasurementTests.cs` |
| TeamCity is not detected | `Baselines/BaselineResolverTests.cs`: set a TeamCity variable, assert no rung fires |
| `--artifacts-path` narrows discovery | `Assemblies/AssemblyDiscoveryTests.cs`, alongside `The_output_hint_breaks_it_too` |
| `unmapped-deterministic-source-root` carries `data.roots`, and fires for packages folders configured in the other supported ways | `Assemblies/CorrespondenceTests.cs` |

For the last row, read the code that raises the notice and the schema's description to find "the
other ways", and add one test per way. The notice came from coding-standards ticket 19
(`.scratch/coding-standards/issues/19-a-wrong-deterministic-root-is-detected.md`), which records
what it covers.

## Acceptance criteria

- Each claim has a named test that fails if it stops being true. Make each one fail once by hand
  to check it.
- No production behaviour changes. If a claim turns out false, record it in this ticket's comments
  as a finding for ticket 16, and leave the test asserting the documented behaviour, skipped with
  the reason.

## Out of scope

`changeSources`, `buildMode`, `forwardedBuildArguments`, `correspondence` (ticket 15, through the
example report). Exit paths (ticket 13).
