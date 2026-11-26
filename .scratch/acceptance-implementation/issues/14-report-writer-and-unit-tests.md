# Report-writer and unit-level tests

Status: resolved
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

## Comments

**Done.** Test code only; no production change. Each test was made to fail once by a temporary
mutation of the production code (dropping the skip check, emitting `none` for no delivery, the
un-kebabed enum name, `AllTests.Count` for `willRun`, five decimals, a TeamCity rung, ignoring
`ArtifactsPath`, renaming `data.roots`, looking in the configured packages folders), then reverted.

| Claim | Test |
|---|---|
| `runnerHost` | `Reporting/ReportEntryTests.A_filtered_entry_names_dotnet_test_as_its_runner_host`, `A_skip_entry_carries_no_runner_host`, `A_project_run_in_full_still_names_its_runner_host` |
| `delivery` | `Reporting/ReportEntryTests.A_short_selection_is_delivered_inline`, `A_selection_merged_into_a_runsettings_file_is_delivered_by_run_settings`, `A_skip_entry_carries_no_delivery`, `The_empty_entries_of_an_underivable_moniker_carry_no_delivery` |
| `willRun` 0 on an unknown total | `Reporting/ReportEntryTests.An_entry_whose_total_is_unknown_will_run_zero` |
| `ratio` to four places | `Reporting/MeasurementTests.The_ratio_is_rounded_to_four_places_in_the_report` |
| TeamCity not detected | `Baselines/BaselineResolverTests.TeamCity_is_not_detected` |
| `--artifacts-path` narrows discovery | `Assemblies/AssemblyDiscoveryTests.The_artifacts_path_hint_breaks_it_too` |
| `data.roots` | `Assemblies/CorrespondenceTests.The_unmapped_root_notice_carries_every_root_it_left_unmapped_as_data` |
| Packages folders configured other ways | `Assemblies/CorrespondenceTests.Package_content_in_a_global_packages_folder_set_in_nuget_config_raises_the_notice`, `Package_content_in_a_RestorePackagesPath_folder_raises_the_notice`, `Package_content_in_a_fallback_packages_folder_raises_the_notice` |

`ReportEntryTests` is a new class beside `ReportTests`. It renders synthesised selections, builds
the report, and asserts on the serialised JSON, because "absent" is a claim about the document.
"The other ways" come from `docs/limitations.md` and ADR-0018: `globalPackagesFolder`,
`RestorePackagesPath`, and fallback folders. `SourceRoots.Of` reads only `NUGET_PACKAGES`, else
`~/.nuget/packages`. Each test writes the configuration file and scans through the same seam
discovery uses.

**Findings for ticket 16:** none. Every claim held as documented.
