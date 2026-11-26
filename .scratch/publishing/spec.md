# Publishing

Status: ready-for-human

The first release of Reach: `dotnet-reach` `0.1.0-alpha.1` on NuGet.org. Everything here except
the publication itself is built. What remains is in [ticket 01](issues/01-publish-0.1.0-alpha.1.md),
and only a human can do it.

This spec was put together on 2026-11-26 from the finished efforts it depended on, which were then
removed from `.scratch/`. Their full text stays in git history:

- [the walking-skeleton spec](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/walking-skeleton/spec.md), §16.4 for the
  publishing decisions in §2 here;
- [the acceptance-layer spec](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-layer/spec.md), §7 and §9 for the
  release gate in §3 here. A section number this document does not have (§1.3, §2.x, §4.4, §4.5, §5.2, §8.2, §10.3)
  is the acceptance-layer spec's, and is marked as such;
- [the M1 implementation tickets](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/m1-implementation/README.md) and
  [the acceptance implementation tickets](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-implementation/README.md). A bare
  "implementation ticket NN" below is one of the latter.

**The row numbers in §3 are the ones the code uses.** The acceptance tests, the sample solutions
under `tests/samples/` and their patches cite "row N", and this is where row N is defined.

## 1. Goal

`dotnet-reach` on NuGet.org at `0.1.0-alpha.1`, so that the `dnx` quickstart the documentation
already contains is real rather than aspirational. The release waits for the gate (§3): every
claim in the docs proven by a passing test. The gate audit (implementation ticket 16) filled §3.6,
and its two follow-ups (implementation tickets 21 and 22) have landed, so the gate is met.

## 2. Decisions

From walking-skeleton spec §16.4 and M1 ticket 21.

- **NuGet.org.** `dnx` resolves from the ambient `nuget.config` hierarchy, and GitHub Packages'
  NuGet feed requires authentication whether or not the package is public, so a `dnx` quickstart
  on it would work only for its author.
- **`release.yml`, on a `v*` tag push**: build, test, pack, push. It is written, and it fails if
  the tag does not match `Version` in `Directory.Build.props`.
- **Trusted Publishing** via `NuGet/login@v1` with `permissions: id-token: write`, because new
  NuGet.org API keys are capped at 30 days. No API key secret exists in the repository.
- **The version is hand-edited** in `Directory.Build.props`, first value `0.1.0-alpha.1`. No MinVer
  or Nerdbank.GitVersioning. A published package **cannot be deleted, only unlisted**, so two
  deliberate human acts (editing the version, pushing the tag) are the right friction for an
  irreversible one.
- **No signing certificate.** nuget.org repository-signs automatically, and registering a
  certificate makes signing mandatory for the account from then on.
- **Install guidance leads with `dnx dotnet-reach@<version>`, never `-g`.** The pin is a
  requirement: `dnx` with a bare package id will not find a prerelease, and fails with a misleading
  "not found in NuGet feeds". `Reporting/WorkflowParityTests` asserts every documented `dnx` line
  pins the version this repository builds.
- **`dogfood.yml` runs Reach on Reach and does not gate.** The selection runs alongside the whole
  suite, and only the whole suite can fail the job. Gating on Reach's own selection is the unproven
  narrowing PRD §8 forbids. Until the package exists, its selection step fails at `dnx`, and the
  workflow lets that through.
- **No `PackageReadmeFile`.** nuget.org does not resolve relative links and the README is mostly
  links into `docs/`; `PackageProjectUrl` takes a reader to them instead.
- **Deliberately not done**: no `.snupkg` or SourceLink, no Dependabot, no coverage gate, no branch
  protection requiring approvals. Do not add them as part of the release.
- **`dotnet reach` dispatch depends only on `ToolCommandName`**, not on the package id, so the
  package name and the verb are independent choices.

## 3. The release gate

### 3.1 The rules

From acceptance-layer [ticket 03](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-layer/issues/03-capabilities-and-the-release-gate.md) and
[ticket 12](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-layer/issues/12-test-it-or-cut-it.md).

- **The gate is every claim the alpha's docs make.** A claim nobody will gate on comes out of the
  docs.
- **The docs in the gate** are `README.md`, `docs/adopting-reach.md`, `docs/report-schema.md`,
  `select --help`, and `docs/limitations.md` **only for its "Detectable" claims that name a notice
  code**. The register's gap descriptions and directions of failure are disclaimers, not promises.
- **A claim may be proven by any suite.** Acceptance tests run Reach in-process, so exit codes,
  argument parsing and delivery mechanics belong to the CLI and unit suites.
- **Claims about other people's tools are cited facts, outside the gate.** Each carries a source
  link. A claim without one is cut.
- **Known-gap tests are never in the gate.**
- **The table in §3.6 is hand-kept.** Each gate claim names the test that proves it. The parity
  tests (acceptance-layer §10.3) cover every part that can be checked automatically. The gate
  audit (implementation ticket 16) replaced every "ticket NN" in the table with the test's real
  name.

Each row is one acceptance test unless it says otherwise. **The patch's subject is a suggestion.**
The claim, the sample and the expected sections are the spec. Where a row says *pin what Reach
does*, the implementer confirms the outcome against Reach. If Reach contradicts the docs, that is a
finding to report, not a change to Reach's output (acceptance-layer §1.3).

### 3.2 The rows: release gate, small samples

| # | Capability | Sample | Sections beyond the required four |
|---|---|---|---|
| 1 | A change selects exactly the tests that reach it through compiled edges | Basics | — |
| 2 | A test whose own body changed is selected | Basics | — |
| 3 | An existing non-test method, body unchanged, that gains `[Fact]` is selected | Basics | — |
| 4 | Interface or virtual dispatch selects every candidate implementation | Basics | — |
| 5 | A deleted method widens its declaring type | Basics | `Tiers` (`whole-type`) |
| 6 | A move to another directory, same namespace, widens that type | Basics | `Tiers` (`whole-type`) |
| 7 | Each dialect is recognised and renders its own filter; NUnit's `~` runs more than it selects | Frameworks | `WillRun`, `Notices` |
| 8 | A test project on an unrecognised framework runs in full, with an unknown total | Frameworks | `Notices` |
| 9 | A multi-targeted test project gives one entry per framework, each pinned with `-f` | Basics † | keys `Basics.Tests@net8.0`, `@net10.0` |
| 10 | A platform-suffixed framework makes its project run in full | Basics † | `Notices` |
| 11 | `xunit.v3` 4.0.0 without the runner setting gets its notice | Basics † | `Notices` |
| 12 | A generator project is in scope, and a change to it widens its consumers | Basics | — |
| 13 | A formatting-only change gives `no-changes` | Basics | `Notices`, `Reasons` (none) |
| 14 | A docs-only change gives `nothing-selected` | Basics | `Notices` (`unmapped-file-no-project`), `Reasons` (`changes-were-formatting-only`) |
| 15 | A changed member that reaches no test is listed with a count of 0 | Basics | `Changes`, `Notices`, `Reasons` (`no-changed-member-reached-a-test`) |
| 16 | A new C# file not tracked by git is flagged | Basics | `Notices` |

Notes on specific rows:

- **Row 3** is the case walking-skeleton ticket 07 escalated. A brand-new test method is not tested
  separately: it is a strict superset (new declaration and new body).
- **Row 7's** patch is the `Tax` change, and it is **the same patch file** `ExampleReportTests`
  uses (acceptance-layer §8.2).
- **Row 12's** `Because` says why it also proves the generator is not expected on disk: if it were,
  every Basics test would end `failed`.
- **Rows 14, 15 and 29** give `Notices` as well as `Reasons`, so they prove the reason codes go in
  `reasons` and not in `notices`.
- **Rows 13 and 14 were the other way round** in the first draft, and implementation ticket 07
  found it. A formatting-only `.cs` edit leaves the changed set empty, so it is `no-changes` with
  no reason. A README is an unmappable path routed to no project, so it is `nothing-selected`,
  flagged `unmapped-file-no-project`, with the reason `changes-were-formatting-only`. That reason
  fires whenever no member changed but some path did, whatever its name says. The docs were
  corrected to match Reach (implementation ticket 16).
- **Row 12** changes a generator's `.cs` source. Implementation ticket 18 made Reach widen a
  generator's consumers for any change inside it, not only its project file.

† Via a baseline patch:

- **Row 9**: retarget `Basics.Core` **and** `Basics.Tests` to `net8.0;net10.0` (a `net8.0` test
  project cannot reference a `net10.0`-only library). The change patch is a body change.
- **Row 10**: retarget the same two projects to `net10.0;net10.0-windows` with
  `<EnableWindowsTargeting>true</EnableWindowsTargeting>`. Without that setting the Linux leg fails
  with `NETSDK1100`.
- **Row 11**: delete `global.json`. The change patch is any one-line body change.

### 3.3 The rows: release gate, the multi-project sample

Each of these is about what is **not** selected. All on Ledger.

| # | Patch | Selected | **Not selected** | Extra sections |
|---|---|---|---|---|
| 17 | Body of `Invoice.DaysOverdue` (Domain), called only by Dunning | its Domain test, the Dunning tests reaching it, Api's reminders test | Billing.Tests, the other Domain tests, Api's posting and health tests | — |
| 18 | A `const` in Billing (`TaxRules.StandardRatePercent`) | **all** Billing.Tests, **all** Api.Tests | Domain.Tests, Dunning.Tests | `Tiers`, `Notices` (`recompilation-widening`) |
| 19 | Remove an unused overload in Dunning (`ReminderSchedule.Next(DateOnly)`) | all Dunning.Tests, all Api.Tests | Domain.Tests, Billing.Tests | `Tiers`, `Notices` (`recompilation-widening`) |
| 20 | `Ledger.Billing.csproj` (a harmless `<NoWarn>`) | Billing.Tests, Api's posting test | Domain, Dunning, Api's reminders and health tests | — |
| 21 | `src/Collections/Directory.Build.props` | Dunning.Tests, Api's reminders test | Domain, Billing, Api's posting and health tests | — |
| 22 | Move a Billing type to another namespace | as row 20 | as row 20 | `Tiers` (`whole-assembly`) |
| 23 | A root file that matches no rule (`CODEOWNERS`) | nothing | everything | `Notices` |

- Rows 18 and 19 select **all** of `Ledger.Api.Tests`, health test included, because that project
  itself transitively references the declaring assembly and is widened whole. Rows 20 and 22 do
  not. The contrast is worth showing.
- **Row 18 once also selected `Ledger.Dunning.Tests.ReminderSenderTests`.** Api is widened whole,
  and with `WebhookChannel` in Api the reverse walk came back into Dunning through
  `ReminderSender`'s `IReminderChannel.Send` call: row 26's interface fan-out reaching into row 18.
  The owner chose to reshape Ledger (acceptance-layer §2.5): `WebhookChannel` moved to `Ledger.Webhooks`, and row 18
  now selects exactly this table (implementation ticket 21).
- **Row 22's `Tiers` include a field at `whole-assembly`.** A field with no initialiser has no
  compiled body to join, so it falls back to whole-assembly widening. It widens the assembly the
  moved type already widened, so the selection is unchanged. `docs/limitations.md` registers the
  fall-back ("A changed declaration with no compiled body widens its whole assembly").
- **Row 23: pin what Reach does.** `ReportBuilder.OutcomeOf` returns `no-changes` when the change
  set is empty, so `CODEOWNERS` may report `no-changes` rather than `nothing-selected` with
  `unmapped-file-no-project`. The docs state row 23 as intended behaviour, so a contradiction is a
  finding.

### 3.4 The rows: known gaps, not in the gate

| # | Gap | Sample |
|---|---|---|
| 24 | Generic instantiations are collapsed: a `Handler<Foo>` change also selects the users of `Handler<Bar>` | Basics |
| 25 | An interpolated `ToString` is not selected | Basics |
| 26 | An interface-typed receiver fans out to every implementer: an `EmailChannel.Send` change still selects Api's reminders test, which uses `WebhookChannel` | Ledger |
| 27 | Recompilation widening's blast radius: a `Money.DefaultCurrency` change selects all 12 tests. Gives `Changes`, to show the one `const` that did it | Ledger |

### 3.5 The rows: two added late

Acceptance-layer [ticket 12](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-layer/issues/12-test-it-or-cut-it.md) decided two things that need a row it did not
number. Both are Basics, release gate.

| # | Capability | Sections beyond the required four |
|---|---|---|
| 28 | A change that breaks the build gives outcome `failed` and exit 2 | `ExitCode` = 2 |
| 29 | A change only to a project no test project reaches gives `nothing-selected` | `Notices`, `Reasons` (`all-changes-outside-analysis-scope`) |

- **Row 28** relies on acceptance-layer §5.2: the harness's own build fails, so Reach runs in
  default build mode.
- **Row 29** changes `Basics.Tools`. Acceptance-layer ticket 12 said the `nothing-selected` rows
  prove all three reason codes; rows 14 and 15 cover only two, so this row covers the third. **Pin
  what Reach does**: if `Basics.Tools` raises a standing notice, it goes behind a baseline patch
  instead (acceptance-layer §2.2).
  Implementation ticket 19 made Reach mark a change outside the analysis scope as such, so this row
  gives `all-changes-outside-analysis-scope`.

### 3.6 Claim to suite

Test names are relative to `tests/Reach.Tests/`. "Row N" is §3.2 to §3.5, and the first table
below names each row's test. "Every acceptance test" means the `Expect` invariants
(acceptance-layer §4.4), which `Harness/ExpectationTests` proves can fail. Filled in by implementation ticket 16, the gate
audit; claims cut by implementation ticket 05 are gone from the table.

**Rows to tests.** `B` is `Acceptance/Basics/BasicsTests`, `F` is
`Acceptance/Frameworks/FrameworksTests`, `L` is `Acceptance/Ledger/LedgerTests`.

| Row | Test |
|---|---|
| 1 | `B.A_change_selects_exactly_the_tests_that_reach_it_through_compiled_edges` |
| 2 | `B.A_test_whose_own_body_changed_is_selected` |
| 3 | `B.An_existing_method_that_gains_Fact_is_selected` |
| 4 | `B.Interface_and_virtual_dispatch_select_every_candidate` |
| 5 | `B.A_deleted_method_widens_its_declaring_type` |
| 6 | `B.A_move_to_another_directory_in_the_same_namespace_widens_that_type` |
| 7 | `F.Each_dialect_renders_its_own_filter_and_NUnit_runs_more_than_it_selects` |
| 8 | `F.A_project_on_an_unrecognised_framework_runs_in_full_with_an_unknown_total` |
| 9 | `B.A_multi_targeted_test_project_gives_one_entry_per_framework` |
| 10 | `B.A_platform_suffixed_framework_makes_its_project_run_in_full` |
| 11 | `B.An_xunit_v3_4_project_without_the_runner_setting_gets_its_notice` |
| 12 | `B.A_change_to_a_generators_source_widens_its_consumers` |
| 13 | `B.A_formatting_only_change_is_no_changes` |
| 14 | `B.A_docs_only_change_selects_nothing_and_says_no_member_changed` |
| 15 | `B.A_changed_member_that_reaches_no_test_is_listed_with_a_count_of_zero` |
| 16 | `B.A_new_source_file_not_tracked_by_git_is_flagged` |
| 17 | `L.A_domain_change_reached_only_through_dunning_leaves_billing_out` |
| 18 | `L.A_changed_const_widens_only_the_projects_that_reference_its_assembly` |
| 19 | `L.A_removed_member_widens_only_the_projects_that_reference_its_assembly` |
| 20 | `L.A_project_file_change_widens_its_project_and_leaves_its_sibling_out` |
| 21 | `L.A_directory_build_props_change_widens_only_the_projects_beneath_it` |
| 22 | `L.A_namespace_move_widens_the_whole_assembly_and_no_further` |
| 23 | `L.A_root_file_matching_no_rule_selects_nothing_and_says_so` |
| 28 | `B.A_change_that_breaks_the_build_fails_with_exit_code_2` |
| 29 | `B.A_change_only_to_a_project_no_test_reaches_selects_nothing` |

Known gaps, outside the gate (acceptance-layer §4.5): 24 `B.Known_gap_a_change_reached_only_through_Handler_of_Foo_selects_the_users_of_Handler_of_Bar`,
25 `B.Known_gap_a_ToString_reached_only_through_interpolation_selects_nothing`,
26 `L.Known_gap_a_change_to_one_channel_selects_the_callers_of_every_channel`,
27 `L.Known_gap_a_changed_const_in_the_core_assembly_selects_every_test`.

**`README.md`**

| Claim | Proven by |
|---|---|
| Walks the call graph backwards from changed code to tests | `Selection/SelectionTests.A_change_two_hops_from_a_test_selects_it`; row 1 |
| Over-selects where the implementation is not knowable | `Graph/WideningTests`; row 4 |
| Never narrows on unproven evidence | a principle: `Projects/AnalysisScopeTests.Scope_is_never_narrowed_to_the_dependents_of_a_changed_project`, and its instances, rows 17–23 |
| An empty selection exits `0` | every `nothing-selected` acceptance test (exit-code invariant): rows 14, 15, 29 |
| `dnx dotnet-reach@0.1.0-alpha.1` is the version this repo builds | `Reporting/WorkflowParityTests.Every_documented_dnx_line_pins_the_version_this_repository_builds` |
| The quick start's filter recipe | replaced by a link to `adopting-reach.md`'s recipe (implementation ticket 05), proven there below |
| The command line documents itself | `Cli/HelpGoldenTests.The_select_help_text_has_not_changed_by_accident`; `Reporting/HelpOptionParityTests.The_docs_describe_exactly_the_options_select_help_lists` |

**`docs/adopting-reach.md`**

| Claim | Proven by |
|---|---|
| A bare package id fails in `dnx` with *"not found in NuGet feeds"* | cited fact: `dotnet tool exec` docs and the SDK's message |
| The exit-code set is 0/1/2/3/4/5/70 | `Cli/ExitCodeTests.The_set_is_exactly_the_one_the_specification_names` |
| 1: usage error, writes no report | `Cli/CommandLineTests.An_unknown_option_is_a_usage_error`, `A_usage_error_writes_nothing_into_the_directory_Reach_owns` |
| 2: build failed | row 28; `Build/BuildAdapterTests.A_failed_build_is_a_result_carrying_what_the_build_said` |
| 3: missing or ambiguous assembly | `Assemblies/AssemblyDiscoveryTests.Debug_and_Release_both_present_is_exit_3_naming_the_option_that_settles_it`, `A_missing_expected_assembly_is_exit_3`; `Cli/CommandLineTests.Nothing_built_under_no_build_is_exit_3_and_still_writes_a_report` |
| 4: baseline unresolvable or shallow, prints the line to add | `Baselines/BaselineResolverIntegrationTests.A_shallow_clone_is_exit_4`; `Baselines/BaselineResolverTests.Exit_4_under_GitHub_Actions_prints_the_line_to_add`, `Exit_4_under_Azure_DevOps_prints_the_line_to_add`, `Exit_4_off_a_known_provider_says_how_to_fix_it_anyway`; `Cli/CommandLineTests.An_unresolvable_baseline_is_exit_4`, `Exit_4_under_a_simulated_GitHub_Actions_environment_prints_the_line_to_add` |
| 5: source and binary do not correspond | `Assemblies/CorrespondenceInBothModesTests.An_edited_file_under_no_build_is_exit_5_naming_the_document` |
| 70: internal error | `Cli/ExitPathTests.A_pipeline_that_throws_is_exit_70` |
| An empty selection emits no command | `Rendering/InvocationTests.An_empty_selection_emits_no_command_at_all`; `Rendering/ConsumerLoopTests.A_loop_over_an_empty_selection_starts_no_process_at_all`; every acceptance test (skip invariant) |
| Every chunk carries at least one test | `Rendering/RenderingTests.The_chunker_partitions_the_set_with_nothing_dropped_or_duplicated`, `An_empty_selection_produces_no_chunks` |
| A multi-targeted project pins its framework | `Rendering/InvocationTests.Every_invocation_for_a_multi_instance_project_carries_a_matching_selector`; row 9 |
| `--ignore-exit-code 8` and the .NET 11 change; `--zero-tests-policy strict` | cited facts: MTP docs, `dotnet test` (MTP) docs, microsoft/testfx#7457 |
| A VSTest project under a testing-platform `global.json` cannot run | cited fact: `dotnet test` (MTP) docs. Reach's part (no check, no notice) is a disclaimer, registered in `limitations.md` (owner decision, implementation ticket 16) |
| A shallow checkout is exit 4 | `Baselines/BaselineResolverTests.A_shallow_clone_is_exit_4_before_anything_else_is_tried`; `Baselines/BaselineResolverIntegrationTests.A_shallow_clone_is_exit_4` |
| The recipe is `dogfood.yml`, and one character of drift fails | `Reporting/WorkflowParityTests.The_documented_recipe_is_the_workflow_this_repository_runs`, `One_changed_character_in_the_documented_recipe_fails_the_comparison`, `The_documented_recipe_fetches_the_whole_history`, `The_documented_recipe_pins_the_version` |
| The recipe's loop runs nothing when empty, propagates failure, is not swallowed by stdin | the dogfood workflow runs it. **Proven only once `0.1.0-alpha.1` is published**: until then the selection step fails at `dnx`, which the workflow lets through, so it does not block publishing |
| `dnx` is put on `PATH` by the .NET 10 SDK installer | cited fact: `dotnet tool exec` docs |
| `jq` is preinstalled; `@sh` quotes argv | cited facts: the runner image readme, the jq manual |
| Detection variables for five providers | `Baselines/BaselineResolverTests.Rung_1_is_GITHUB_BASE_REF`, `Rung_2_is_the_Azure_DevOps_target_branch`, `Rung_3_is_the_GitLab_merge_request_target`, `Rung_4_is_the_Bitbucket_destination_branch`, `Rung_5_is_the_Jenkins_change_target`, `The_option_wins_over_every_variable` |
| Fetch-history settings for the five non-GitHub providers | cited facts: each provider's docs |
| TeamCity is not detected | `Baselines/BaselineResolverTests.TeamCity_is_not_detected` |
| Never reads a source-branch variable | `Baselines/BaselineResolverTests.GITHUB_HEAD_REF_is_never_read` and its five siblings |
| `-c` and `-v` take `dotnet build`'s values; `--no-color` turns colour off | `Cli/CommandLineTests.Verbosity_mirrors_the_SDKs_spellings`, `Every_option_reaches_the_request`, `No_color_and_NO_COLOR_and_a_redirected_stdout_each_suppress_colour` |
| `--no-build` checks checksums in both modes | `Assemblies/CorrespondenceInBothModesTests` (all three) |
| Git does not preserve modification times; MSBuild's incremental build compares timestamps | cited facts: the Git FAQ, MSBuild's incremental-builds docs |
| `--runsettings` merges; an existing filter runs the project in full | `Rendering/InvocationTests.A_runsettings_file_without_a_filter_is_merged`, `A_runsettings_file_that_already_filters_downgrades_to_run_all` |
| `-o` and `--artifacts-path` narrow the scan | `Assemblies/AssemblyDiscoveryTests.The_output_hint_breaks_it_too`, `The_artifacts_path_hint_breaks_it_too` |
| `--report` / `--report-dir` are where Reach writes | `Cli/CommandLineTests.Report_dir_relocates_the_whole_directory_including_side_car_files`, `Report_moves_the_json_alone_and_leaves_the_directory_where_it_was` |
| A layout switch after `--` is exit 1 | `Cli/CommandLineTests.A_layout_switch_after_the_boundary_is_exit_1_naming_the_Reach_option`, `A_layout_switch_written_with_an_equals_sign_is_refused_too` |
| A directory move widens the type; a namespace move widens the assembly | `Changes/ChangedSetTests.A_type_moved_between_directories_yields_whole_type_widening`, `A_type_moved_between_namespaces_yields_whole_assembly_widening`; rows 6, 22 |
| `.reach/` and its `.gitignore` on first use; an existing one left alone | `Cli/CommandLineTests.A_run_that_gets_past_usage_creates_the_directory_with_a_gitignore`, `An_existing_gitignore_is_left_alone` |
| Reach never deletes its files | `Rendering/InvocationTests.Re_running_overwrites_a_side_car_rather_than_accumulating`; `Acceptance/ReachDirectorySurvivalTests.The_report_and_side_car_files_remain_after_the_process_exits` |

**`docs/report-schema.md`**

| Claim | Proven by |
|---|---|
| `example-report.json` is the determinism fixture's output | `Acceptance/ExampleReportTests.The_committed_example_is_what_row_7s_patch_produces` |
| `schemaVersion` is `1` | `Reporting/ReportTests.The_schema_version_is_one_and_cannot_be_anything_else` |
| Deterministic; arrays sorted; only timings differ | `Reporting/ReportTests.The_same_input_twice_produces_byte_identical_json`, `Every_array_is_sorted_by_its_documented_key`, `Only_the_timings_differ_between_two_runs`; every acceptance test |
| A report is written for every outcome, including failure | exit 0: `Cli/CommandLineTests.A_complete_run_exits_0_and_writes_a_report`; exit 3: `Nothing_built_under_no_build_is_exit_3_and_still_writes_a_report`; exits 2, 4, 5, 70: `Cli/ExitPathTests.Exit_2_writes_a_failed_report_with_no_summary`, `Exit_4_…`, `Exit_5_…`, `Exit_70_…` |
| …except exit 1 | `Cli/CommandLineTests.A_usage_error_writes_nothing_into_the_directory_Reach_owns` |
| `outcome` is a closed set of four | `Reporting/ReportTests.The_four_outcomes_are_a_closed_set` |
| `reasons` present and non-empty exactly when `nothing-selected` | `Reporting/ReportTests.Nothing_selected_always_carries_a_non_empty_reasons_array`, `A_selected_run_carries_no_reasons`; rows 14, 15, 29 |
| `scope` carries identities, never closure edges | `Reporting/ReportTests.Scope_carries_identities_and_never_closure_edges` |
| `summary` absent when the run failed before rendering | `Cli/ExitPathTests.Exit_2_writes_a_failed_report_with_no_summary` and its exit 4, 5 and 70 siblings |
| `no-changes` and `nothing-selected` differ; same exit | `Reporting/ReportTests.No_changes_and_nothing_selected_are_different_outcomes`; `Reporting/HumanSummaryTests.No_changes_and_nothing_selected_get_visibly_different_verdicts`; exit-code invariant |
| A formatting-only C# edit is `no-changes`; a README-only change is `nothing-selected` | rows 13, 14 |
| On `no-changes` every project is `skip` with zero invocations | `Reporting/ReportTests.No_changes_still_emits_every_test_project_as_skip`; `Rendering/ConsumerLoopTests.A_loop_over_a_no_changes_report_starts_no_process_either`; row 13 |
| `envelope.baseline` is a SHA with `detectedFrom` and `isHead` | `Baselines/BaselineResolverTests.Every_resolution_is_disclosed_with_the_sha_it_landed_on`, `A_baseline_resolving_to_HEAD_emits_its_own_notice` |
| …absent on exit 4; `envelope.head` absent when git cannot be read | `Cli/ExitPathTests.Exit_4_writes_no_baseline_into_the_envelope`, `A_head_git_cannot_read_is_absent_from_the_envelope` |
| `changeSources`, `buildMode`, `forwardedBuildArguments`, `correspondence` in the report | `Acceptance/ExampleReportTests.The_committed_example_is_what_row_7s_patch_produces` (the example carries all four) |
| One entry per (project, framework) | `Reporting/ReportTests.A_single_targeted_project_still_produces_a_project_and_framework_keyed_entry`; row 9 |
| `framework` and `dialect` both absent when unrecognised, equal otherwise | `Acceptance/ExampleReportTests.The_committed_example_is_what_row_7s_patch_produces` (acceptance-layer §8.2); `Reporting/WalkThroughParityTests.Every_value_the_walk_through_quotes_is_the_example_s` |
| `runnerHost`: its value, absent on `skip`, present on run-all | `Reporting/ReportEntryTests.A_filtered_entry_names_dotnet_test_as_its_runner_host`, `A_skip_entry_carries_no_runner_host`, `A_project_run_in_full_still_names_its_runner_host` |
| `delivery` values; absent on an entry with no command | `Rendering/InvocationTests.A_long_selection_on_the_testing_platform_goes_through_a_response_file`, `A_long_selection_on_a_host_with_no_channel_is_chunked`; `Reporting/ReportEntryTests.A_short_selection_is_delivered_inline`, `A_selection_merged_into_a_runsettings_file_is_delivered_by_run_settings`, `A_skip_entry_carries_no_delivery` |
| `invocations`: many when chunked, one for run-all, zero for skip | `Rendering/InvocationTests.A_long_selection_on_a_host_with_no_channel_is_chunked`, `Run_all_emits_one_invocation_with_no_filter`; `Reporting/ReportTests.Invocations_is_always_an_array_and_empty_for_skip` |
| An underivable moniker runs once, the rest empty with no `delivery` | `Rendering/InvocationTests.A_multi_instance_project_with_a_platform_suffix_runs_project_wide_once`; `Reporting/ReportEntryTests.The_empty_entries_of_an_underivable_moniker_carry_no_delivery` |
| `counts.total` is `"unknown"`, never `0` | `Reporting/ReportTests.An_unknown_total_round_trips_and_is_distinguishable_from_zero`; row 8; every acceptance test |
| …and `willRun` is `0` on that entry | `Reporting/ReportEntryTests.An_entry_whose_total_is_unknown_will_run_zero` |
| `projectsRunInFull` counts those entries | `Reporting/MeasurementTests.An_unenumerable_project_contributes_to_neither_side`, `The_summary_line_carries_both_numbers` |
| `tests[].rules` and their three values | `Selection/SelectionTests.A_changed_test_method_selects_itself`, `A_newly_added_test_selects_with_new_since_baseline_and_an_empty_roots_list`; rows 1–3 |
| `changes` empty and `pathClass` absent exactly when not `reverse-reachable` | `Selection/SelectionTests.A_newly_added_test_selects_with_new_since_baseline_and_an_empty_roots_list`, `A_test_selected_by_a_change_elsewhere_carries_a_root_and_a_class` |
| `pathClass` is the weakest edge on the strongest path | `Selection/ReverseWalkTests.A_test_with_both_a_compiled_and_a_widened_path_reads_compiled`, `A_test_reachable_only_through_a_widened_edge_reads_widened` |
| `paths` only under `--paths`, every hop a method | `Selection/SelectionTests.Paths_are_off_by_default`, `Paths_produce_a_hop_sequence_from_the_test_back_to_the_change` |
| `tests[].id` is stable for the same input | `Reporting/ReportTests.The_same_input_twice_produces_byte_identical_json`; every acceptance test (determinism) |
| `changes` keyed on changes, not expanded roots | `Selection/WholeProjectAndWideningTests.Whole_assembly_widening_contributes_one_entry_whatever_it_expands_to` |
| `tier` values | `Selection/SelectionTests.A_member_change_is_tiered_as_a_member`; `Changes/ChangedSetTests`; rows 5, 6, 22 |
| `testsReached` counts, including `0` | `Selection/SelectionTests.A_change_reaching_no_test_at_all_appears_with_a_count_of_zero`, `A_change_that_reaches_a_test_carries_the_count`; row 15 |
| A change outside the analysis scope is listed with `testsReached: 0` and a reason saying so | `Reporting/ReportTests.A_change_only_outside_the_analysis_scope_says_so_rather_than_that_it_reached_no_test`, `An_out_of_scope_change_beside_an_in_scope_one_that_reached_nothing_keeps_that_reason`; row 29 |
| `summary.willRun` is the numerator; `widenedPairs` | `Reporting/MeasurementTests.The_numerator_is_what_will_run_not_what_was_selected`, `The_widening_delta_counts_pairs_whose_path_class_is_widened` |
| `ratio` to four places, or `null` | `Reporting/MeasurementTests.Nothing_enumerable_reports_an_unknown_ratio`, `The_ratio_is_rounded_to_four_places_in_the_report` |
| The walk-through's quoted values | `Reporting/WalkThroughParityTests.Every_value_the_walk_through_quotes_is_the_example_s`, `One_changed_value_in_the_walk_through_fails_the_comparison` |
| Every `blind-spot` code has a register entry | `Reporting/RegisterParityTests.Every_blind_spot_code_has_a_register_entry`, `The_parity_test_fails_when_a_blind_spot_code_is_unregistered` |
| The notice index lists every code Reach can emit, under the right kind | `Reporting/NoticeIndexParityTests.The_index_lists_every_code_Reach_can_emit_under_its_kind`, `One_renamed_code_in_the_index_fails_the_comparison`, `A_code_under_the_wrong_kind_fails_the_comparison` |
| Notices cannot be suppressed | `Cli/HelpGoldenTests.The_select_help_text_has_not_changed_by_accident`; `Reporting/HelpOptionParityTests.An_option_the_docs_do_not_describe_fails_the_comparison` |

**`select --help`**

| Claim | Proven by |
|---|---|
| The help text, and the options it lists | `Cli/HelpGoldenTests.The_select_help_text_has_not_changed_by_accident`; `Reporting/HelpOptionParityTests` (all three) |
| The target is discovered in the working directory when omitted | `Projects/TargetDiscoveryTests` |
| `--base` is auto-detected when omitted | `Baselines/BaselineResolverTests.Rung_1_is_GITHUB_BASE_REF` … `Rung_7_is_exit_4` |
| `--report -` writes the JSON to standard output and the summary to standard error | `Cli/CommandLineTests.Report_dash_puts_the_json_on_stdout_and_moves_the_summary_to_stderr` |
| `--report-dir` defaults to `.reach` under the working directory | `Cli/CommandLineTests.The_report_defaults_into_the_directory_Reach_owns` |
| `--no-build` analyses the output on disk without building | `Assemblies/CorrespondenceInBothModesTests.No_build_never_invokes_the_build` |
| `--no-color`, and `NO_COLOR` is honoured | `Cli/CommandLineTests.No_color_and_NO_COLOR_and_a_redirected_stdout_each_suppress_colour`, `An_empty_NO_COLOR_does_not_suppress_colour` |
| `--paths`, `--runsettings`, `-o`, `--artifacts-path` | as `report-schema.md` and `adopting-reach.md` above |

**Per notice code.** Each code is proven by a test that makes Reach emit it and asserts it. These
are also the codes `limitations.md`'s "Detectable" claims name.

| Code | Proven by |
|---|---|
| `ignored-untracked-assembly` | `Assemblies/CorrespondenceTests.A_first_party_document_that_is_untracked_and_ignored_is_disclosed` |
| `unmapped-file-no-project` | `Changes/TierLadderTests.That_case_emits_its_own_notice_and_names_the_file`; rows 14, 23 |
| `parse-failed` | `Changes/ParseHealthTests.A_file_that_does_not_parse_widens_its_project_and_says_so` |
| `unresolved-first-party-member` | `Graph/CallGraphTests.An_unresolvable_first_party_member_is_a_notice_and_does_not_stop_the_run` |
| `signature-ambiguous` | `Graph/CallGraphTests.An_ambiguous_signature_edges_to_every_candidate_and_says_so` |
| `whole-project-fallback` | row 8 |
| `nunit-filter-overmatch`, `dialect-over-selects` | `Rendering/InvocationTests.A_contains_dialect_reports_the_extra_it_will_run`; row 7 |
| `runsettings-filter-conflict` | `Rendering/InvocationTests.A_runsettings_file_that_already_filters_downgrades_to_run_all` |
| `framework-selector-underivable` | `Rendering/InvocationTests.A_multi_instance_project_with_a_platform_suffix_runs_project_wide_once`; row 10 |
| `recompilation-widening` | `Changes/RecompilationWideningTests.Each_triggering_change_raises_its_own_widening_notice_naming_the_member_and_its_consumers`; rows 18, 19 |
| `project-outside-every-solution` | `Projects/AnalysisScopeTests.A_project_outside_every_solution_emits_the_scope_notice_and_continues` |
| `untracked-source-files` | `Changes/ChangedSetTests.An_untracked_source_file_is_disclosed`; row 16 |
| `no-changed-member-reached-a-test` | row 15 (`Reasons`) |
| `all-changes-outside-analysis-scope` | row 29 (`Reasons`); `Reporting/ReportTests.A_change_only_outside_the_analysis_scope_says_so_rather_than_that_it_reached_no_test` |
| `changes-were-formatting-only` | row 14 (`Reasons`) |
| the three above go in `reasons`, not `notices` | rows 14, 15, 29 (exact `Notices` and `Reasons`) |
| `baseline-resolved`, `baseline-is-head`, `baseline-unresolvable` | `Baselines/BaselineResolverTests.Every_resolution_is_disclosed_with_the_sha_it_landed_on`, `A_baseline_resolving_to_HEAD_emits_its_own_notice`, `A_failed_resolution_is_disclosed_too` |
| `langversion-above-ceiling` | `Changes/ParseHealthTests.A_project_declaring_a_language_version_above_the_ceiling_raises_an_environment_notice`; `Projects/AnalysisScopeTests.A_language_version_above_the_ceiling_is_reported_including_from_Directory_Build_props` |
| `selection-delivered-out-of-band` | `Rendering/InvocationTests.A_long_selection_on_the_testing_platform_goes_through_a_response_file`, `A_long_selection_on_a_host_with_no_channel_is_chunked` |
| `test-runner-not-configured` | row 11 |
| `unmapped-deterministic-source-root`, its `data.roots`, and packages folders configured other ways | `Assemblies/CorrespondenceTests.A_further_deterministic_root_is_not_guessed_at_and_says_so`, `The_unmapped_root_notice_carries_every_root_it_left_unmapped_as_data`, `Package_content_in_a_global_packages_folder_set_in_nuget_config_raises_the_notice`, `Package_content_in_a_RestorePackagesPath_folder_raises_the_notice`, `Package_content_in_a_fallback_packages_folder_raises_the_notice` |

## 4. Tickets

| # | Ticket | Status |
|---|---|---|
| 01 | [Publish 0.1.0-alpha.1](issues/01-publish-0.1.0-alpha.1.md) | ready-for-human |
