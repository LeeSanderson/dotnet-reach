# Which suite proves each claim

Type: task
Status: resolved
Blocked by: (none)

## Question

The release gate is **every claim the alpha's docs make, proven by some suite**
([Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md)).
The acceptance layer takes rows 1–23 of that ticket; everything else — exit codes, argument
parsing, `--no-build` and exit 5, `--runsettings`, `--paths`, CI baseline detection, shallow
clones, response-file and chunked delivery, `.reach/.gitignore`, the report-shape claims in
`docs/report-schema.md` — is assumed to be covered by an existing test.

AFK: walk every claim in `README.md`, `docs/adopting-reach.md` and `docs/report-schema.md`,
and name the test (file and method) that proves each one outside the acceptance layer. The
output is the claim-to-suite table the spec will carry, plus the list of claims **no test
proves**.

That list is the input to a decision, not the decision itself: for each unproven claim, the
owner chooses a test or cutting the claim from the docs.

## Answer

Walked on 2026-11-17 against `main` at `2e65fc6`. Every behavioural claim in the three docs is
listed below with the test that proves it. **Acceptance row N** means the claim falls to row N
of [ticket 03](03-capabilities-and-the-release-gate.md). Where a row exists, the in-memory test
named next to it stays in place, and the row is the gate proof.

Strength: **proven** means a test fails if the claim stops being true. **Partial** means a test
covers part of the claim, or covers it at a lower level than the doc states. Everything that is
not proven also appears in the unproven list at the end.

### `README.md`

| Claim | Proven by | Strength |
|---|---|---|
| Walks the call graph backwards from changed code to tests | `Selection/SelectionTests.A_change_two_hops_from_a_test_selects_it`; acceptance row 1 | proven |
| Over-selects where the implementation is not knowable | `Graph/WideningTests` (all); acceptance row 4 | proven |
| Never narrows on unproven evidence | `Projects/AnalysisScopeTests.Scope_is_never_narrowed_to_the_dependents_of_a_changed_project` | partial: a principle; its instances are proven row by row below |
| An empty selection exits `0` | `Cli/ExitCodeTests.An_empty_selection_exits_0` checks only the enum value. `Cli/CommandLineTests.A_complete_run_exits_0_and_writes_a_report` is a *no-changes* run | **partial**: no end-to-end run with outcome `nothing-selected` asserts exit 0 |
| `dnx dotnet-reach@0.1.0-alpha.1` is the version this repo builds | `Reporting/WorkflowParityTests.Every_documented_dnx_line_pins_the_version_this_repository_builds` (scans README and adopting) | proven |
| `dotnet test --filter "$(…)"` | nothing | **unproven**: no option prints a bare filter (see `Cli/Golden/select-help.txt`) |
| The command line documents itself | `Cli/HelpGoldenTests` (both) | proven |

### `docs/adopting-reach.md`

| Claim | Proven by | Strength |
|---|---|---|
| A bare package id fails in `dnx` with *"not found in NuGet feeds"* | nothing | **external**: a claim about `dnx` |
| Exit-code set 0/1/2/3/4/5/70 | `Cli/ExitCodeTests.The_set_is_exactly_the_one_the_specification_names` | proven, as values |
| 1: usage error, writes no report | `Cli/CommandLineTests.An_unknown_option_is_a_usage_error`, `A_usage_error_writes_nothing_into_the_directory_Reach_owns` | proven |
| 2: build failed | `Build/BuildAdapterTests.A_failed_build_is_a_result_carrying_what_the_build_said` | **partial**: no run asserts the process exits 2 |
| 3: missing or ambiguous assembly, names candidates and the settling option | `Assemblies/AssemblyDiscoveryTests.Debug_and_Release_both_present_is_exit_3_naming_the_option_that_settles_it`, `A_missing_expected_assembly_is_exit_3`; `Cli/CommandLineTests.Nothing_built_under_no_build_is_exit_3_and_still_writes_a_report` | proven |
| 4: baseline unresolvable, shallow clone, prints the line to add | `Baselines/BaselineResolverIntegrationTests.A_shallow_clone_is_exit_4`; `BaselineResolverTests.Exit_4_under_GitHub_Actions_prints_the_line_to_add` (+ Azure, + unknown provider); `Cli/CommandLineTests.An_unresolvable_baseline_is_exit_4` | proven |
| 5: source and binary do not correspond | `Assemblies/CorrespondenceInBothModesTests.An_edited_file_under_no_build_is_exit_5_naming_the_document` | proven |
| 70: internal error | nothing beyond the enum value | **unproven**: no test makes an unhandled failure exit 70 |
| Reach never emits an invocation whose filter matches nothing: empty selection emits no command | `Rendering/InvocationTests.An_empty_selection_emits_no_command_at_all`; `ConsumerLoopTests.A_loop_over_an_empty_selection_starts_no_process_at_all` | proven |
| …every chunk carries at least one test | `Rendering/RenderingTests.The_chunker_partitions_the_set_with_nothing_dropped_or_duplicated`, `An_empty_selection_produces_no_chunks` | proven |
| …a multi-targeted project pins its framework | `Rendering/InvocationTests.Every_invocation_for_a_multi_instance_project_carries_a_matching_selector`; acceptance row 9 | proven |
| `--ignore-exit-code 8` changes the module line and stopped working on .NET 11; `--zero-tests-policy strict` | nothing | **external**: claims about the SDK and the testing platform |
| Shallow checkout → exit 4 | `Baselines/BaselineResolverTests.A_shallow_clone_is_exit_4_before_anything_else_is_tried`; integration twin | proven |
| The recipe is `.github/workflows/dogfood.yml`, and one character of drift fails the build | `Reporting/WorkflowParityTests.The_documented_recipe_is_the_workflow_this_repository_runs`, `One_changed_character_in_the_documented_recipe_fails_the_comparison`, `The_documented_recipe_fetches_the_whole_history` | proven |
| The recipe's loop runs nothing on an empty selection, propagates failure, and is not swallowed by stdin | `Rendering/ConsumerLoopTests` drives a C# loop, not the shell loop | **partial**: the bash/`jq`/`eval` behaviour is unproven. The dogfood workflow runs it, but no test checks it |
| `jq` is preinstalled; `@sh` quotes argv | nothing | **external** |
| Detection variables: GitHub, Azure DevOps, GitLab, Bitbucket, Jenkins | `Baselines/BaselineResolverTests.Rung_1` … `Rung_5`, `The_option_wins_over_every_variable` | proven |
| Fetch-history settings for the five non-GitHub providers | nothing | **external**, and unverified: the page itself warns against unverified recipes |
| TeamCity is not detected | nothing | **unproven** (implied by the rung list, but no test sets a TeamCity variable) |
| Never reads a source-branch variable | `Baselines/BaselineResolverTests.GITHUB_HEAD_REF_is_never_read` and the five siblings | proven |
| `--no-build` checks checksums in both modes; default mode passes after the build | `Assemblies/CorrespondenceInBothModesTests` (all three) | proven |
| `--runsettings` merges; an existing `<TestCaseFilter>` runs the project in full | `Rendering/InvocationTests.A_runsettings_file_without_a_filter_is_merged`, `A_runsettings_file_that_already_filters_downgrades_to_run_all` | proven |
| `-o` / `--artifacts-path` narrow the scan | `Assemblies/AssemblyDiscoveryTests.The_output_hint_breaks_it_too`; `Build/BuildAdapterTests.The_layout_options_reach_the_build` | partial: `--artifacts-path` reaches the build, but nothing shows it narrowing discovery |
| `--report` / `--report-dir` are where Reach writes | `Cli/CommandLineTests.Report_dir_relocates_the_whole_directory_including_side_car_files`, `Report_moves_the_json_alone_and_leaves_the_directory_where_it_was` | proven |
| A layout switch after `--` is exit 1 | `Cli/CommandLineTests.A_layout_switch_after_the_boundary_is_exit_1_naming_the_Reach_option` (theory), `A_layout_switch_written_with_an_equals_sign_is_refused_too` | proven |
| A directory move widens the type; a namespace move widens the assembly | `Changes/ChangedSetTests.A_type_moved_between_directories_yields_whole_type_widening`, `A_type_moved_between_namespaces_yields_whole_assembly_widening`; acceptance rows 6, 22 | proven |
| `.reach/` holds report and response files; `.gitignore` with `*` on first use; existing one left alone | `Cli/CommandLineTests.A_run_that_gets_past_usage_creates_the_directory_with_a_gitignore`, `An_existing_gitignore_is_left_alone`, `Report_dir_relocates…` | proven |
| Reach never deletes them | `Rendering/InvocationTests.Re_running_overwrites_a_side_car_rather_than_accumulating` | partial: files exist after the run, but no test asserts they survive exit |

### `docs/report-schema.md`

| Claim | Proven by | Strength |
|---|---|---|
| `example-report.json` is byte-for-byte the determinism fixture's output | `Acceptance/ExampleReportTests.The_committed_example_is_what_the_determinism_fixture_produces` | proven (rehomed to Frameworks by [ticket 06](06-migrating-the-contoso-tests.md)) |
| `schemaVersion` is `1` and nothing else | `Reporting/ReportTests.The_schema_version_is_one_and_cannot_be_anything_else` | proven |
| Byte-deterministic; arrays sorted by documented key; only timings differ | `Reporting/ReportTests.The_same_input_twice_produces_byte_identical_json`, `Every_array_is_sorted_by_its_documented_key`, `Only_the_timings_differ_between_two_runs`; every acceptance test | proven |
| A report is written for every outcome including failure | exit 3: `Cli/CommandLineTests.Nothing_built_under_no_build_is_exit_3_and_still_writes_a_report`; exit 0: `A_complete_run_exits_0…` | **partial**: no test checks a report is written on exit 2, 4, 5 or 70 |
| …except exit 1 | `Cli/CommandLineTests.A_usage_error_writes_nothing_into_the_directory_Reach_owns` | proven |
| `outcome` is a closed set of four | `Reporting/ReportTests.The_four_outcomes_are_a_closed_set` | proven |
| `reasons` present and non-empty exactly when `nothing-selected` | `Reporting/ReportTests.Nothing_selected_always_carries_a_non_empty_reasons_array`, `A_selected_run_carries_no_reasons` | proven (no-changes and failed are not checked for absence) |
| `scope` carries identities, never closure edges | `Reporting/ReportTests.Scope_carries_identities_and_never_closure_edges` | proven |
| `summary` absent when the run failed before rendering | nothing | **unproven** |
| `no-changes` and `nothing-selected` are different; same exit | `Reporting/ReportTests.No_changes_and_nothing_selected_are_different_outcomes`; `HumanSummaryTests.No_changes_and_nothing_selected_get_visibly_different_verdicts` | proven (same exit: see the README row above) |
| On `no-changes` every project is `skip` with zero invocations | `Reporting/ReportTests.No_changes_still_emits_every_test_project_as_skip`; `ConsumerLoopTests.A_loop_over_a_no_changes_report_starts_no_process_either`; acceptance row 14 | proven |
| `envelope.baseline` resolved to a SHA, with `detectedFrom` and `isHead` | `Baselines/BaselineResolverTests.Every_resolution_is_disclosed_with_the_sha_it_landed_on`, `A_baseline_resolving_to_HEAD_emits_its_own_notice` | proven |
| …absent on exit 4 | nothing | **unproven** |
| `envelope.head` absent when git cannot be read | nothing | **unproven** |
| `changeSources`, `buildMode`, `forwardedBuildArguments` | `Cli/CommandLineTests.Every_option_reaches_the_request`; `Build/BuildAdapterTests.Forwarded_tokens_reach_the_build_verbatim_and_last` | partial: the request carries them, but no test reads them from the report |
| `correspondence` is `verified` · `skipped` · `failed` | `Assemblies/CorrespondenceTests.Source_that_matches_the_recorded_checksums_verifies`, `Nothing_to_check_is_skipped_rather_than_verified`, `Source_edited_after_the_build_is_exit_5_naming_the_document` | proven at the checker. Partial in the report |
| One entry per (project, framework), even single-targeted | `Reporting/ReportTests.A_single_targeted_project_still_produces_a_project_and_framework_keyed_entry`; acceptance row 9 | proven |
| `framework` and `dialect` both absent when unrecognised, and carry the same string otherwise | acceptance row 8 (absence), row 7 (value) | covered by the rows, once rows 7 and 8 assert both fields |
| `packageVersion` is never emitted | nothing | **unproven** |
| `runnerHost` is `dotnet-test`, absent on `skip`, present on run-all | nothing | **unproven** |
| `delivery` values; absent on an entry with no command | `Rendering/InvocationTests.A_long_selection_on_the_testing_platform_goes_through_a_response_file` (`response-file`); `…_is_chunked` | partial: `inline`, `run-settings` and absence are unchecked |
| `invocations` is many when chunked, one for run-all, zero for skip | `Rendering/InvocationTests.A_long_selection_on_a_host_with_no_channel_is_chunked`, `Run_all_emits_one_invocation_with_no_filter`; `Reporting/ReportTests.Invocations_is_always_an_array_and_empty_for_skip` | proven |
| An underivable moniker: one invocation on the first entry, the rest empty with no `delivery` | `Rendering/InvocationTests.A_multi_instance_project_with_a_platform_suffix_runs_project_wide_once`; acceptance row 10 | proven, except the absence of `delivery` |
| `counts.total` is `"unknown"`, never `0` | `Reporting/ReportTests.An_unknown_total_round_trips_and_is_distinguishable_from_zero`; acceptance row 8 | proven |
| …and `willRun` reads `0` on that entry | nothing | **unproven** (an acceptance row 8 expectation could carry it) |
| `projectsRunInFull` counts those entries | `Reporting/MeasurementTests.An_unenumerable_project_contributes_to_neither_side`, `The_summary_line_carries_both_numbers` | proven |
| `tests[].rules` and their three values | `Selection/SelectionTests.A_changed_test_method_selects_itself`, `A_newly_added_test_selects_with_new_since_baseline_and_an_empty_roots_list`; acceptance rows 1–3 | proven |
| `changes` empty and `pathClass` absent exactly when not `reverse-reachable` | `Selection/SelectionTests.A_newly_added_test_selects…`, `A_test_selected_by_a_change_elsewhere_carries_a_root_and_a_class` | proven |
| `pathClass` is the weakest edge on the strongest path | `Selection/ReverseWalkTests.A_test_with_both_a_compiled_and_a_widened_path_reads_compiled`, `A_test_reachable_only_through_a_widened_edge_reads_widened` | proven |
| `paths` only under `--paths`, every hop a method | `Selection/SelectionTests.Paths_are_off_by_default`, `Paths_produce_a_hop_sequence_from_the_test_back_to_the_change` | proven |
| `tests[].id` stable within a run and comparable across runs | determinism tests | partial: same input only |
| `changes` keyed on changes, not expanded roots | `Selection/WholeProjectAndWideningTests.Whole_assembly_widening_contributes_one_entry_whatever_it_expands_to` | proven |
| `tier` values | `Selection/SelectionTests.A_member_change_is_tiered_as_a_member`; `Changes/ChangedSetTests` (whole-type, whole-assembly); acceptance rows 5, 6, 22 | proven |
| `testsReached` counts, including `0` | `Selection/SelectionTests.A_change_reaching_no_test_at_all_appears_with_a_count_of_zero`, `A_change_that_reaches_a_test_carries_the_count`; acceptance row 15 | proven |
| `summary.willRun` is the numerator; `widenedPairs` | `Reporting/MeasurementTests.The_numerator_is_what_will_run_not_what_was_selected`, `The_widening_delta_counts_pairs_whose_path_class_is_widened` | proven |
| `ratio` to four places, or `null` | `Reporting/MeasurementTests.Nothing_enumerable_reports_an_unknown_ratio` | partial: `null` proven, four places not |
| The walk-through's quoted values (`entries[2]`, counts 1/2/3, the notices on `entries[0]` and `entries[1]`, "five-project") | nothing; only the example file is pinned | **unproven**: the prose can drift from the example. [Ticket 06](06-migrating-the-contoso-tests.md) already regenerates both |
| Every `blind-spot` code has a register entry, and the build fails if not | `Reporting/RegisterParityTests.Every_blind_spot_code_has_a_register_entry`, `The_parity_test_fails_when_a_blind_spot_code_is_unregistered` | proven |
| The notice index lists *every code Reach can emit*, under the right kind | `RegisterParityTests.Every_code_in_the_catalogue_has_a_kind` checks the catalogue, not this page | **unproven**: nothing compares the page to the catalogue |
| Notices cannot be suppressed | `Cli/HelpGoldenTests` (no such switch in the help) | proven, indirectly |

**Per notice code.** Each code is proven by a test that makes Reach emit it and asserts it, or
it is not proven.

| Code | Proven by |
|---|---|
| `ignored-untracked-assembly` | `Assemblies/CorrespondenceTests.A_first_party_document_that_is_untracked_and_ignored_is_disclosed` |
| `unmapped-file-no-project` | `Changes/TierLadderTests.That_case_emits_its_own_notice_and_names_the_file`; acceptance row 23 |
| `parse-failed` | `Changes/ParseHealthTests.A_file_that_does_not_parse_widens_its_project_and_says_so` |
| `calli-unresolved` | **unproven, and never emitted**: catalogued only (`NoticeCatalogue.cs:54`) |
| `unresolved-first-party-member` | `Graph/CallGraphTests.An_unresolvable_first_party_member_is_a_notice_and_does_not_stop_the_run` |
| `signature-ambiguous` | `Graph/CallGraphTests.An_ambiguous_signature_edges_to_every_candidate_and_says_so` |
| `whole-project-fallback` | `Acceptance/AcceptanceTests.An_unrecognised_framework_runs_in_full_with_an_unknown_total`; acceptance row 8 |
| `nunit-filter-overmatch`, `dialect-over-selects` | `Acceptance/AcceptanceTests.The_NUnit_filter_renders_with_contains…`; `Rendering/InvocationTests.A_contains_dialect_reports_the_extra_it_will_run`; acceptance row 7 |
| `runsettings-filter-conflict` | `Rendering/InvocationTests.A_runsettings_file_that_already_filters_downgrades_to_run_all` |
| `framework-selector-underivable` | `Rendering/InvocationTests.A_multi_instance_project_with_a_platform_suffix_runs_project_wide_once`; acceptance row 10 |
| `recompilation-widening` | **unproven, and never emitted**: catalogued only (`NoticeCatalogue.cs:47`). `RecompilationWideningTests` prove the widening, not a notice. Acceptance row 18 names this code |
| `project-outside-every-solution` | `Projects/AnalysisScopeTests.A_project_outside_every_solution_emits_the_scope_notice_and_continues` |
| `untracked-source-files` | `Changes/ChangedSetTests.An_untracked_source_file_is_disclosed`; acceptance row 16 |
| `no-changed-member-reached-a-test` | **unproven**: emitted, but no test names it. `ReportTests.Nothing_selected_always_carries…` only checks that a reason exists |
| `all-changes-outside-analysis-scope` | **unproven**: emitted, never asserted |
| `changes-were-formatting-only` | **unproven** today; acceptance row 13 covers it only if its expected result includes `reasons` (see below) |
| the last three go in `reasons`, not `notices` | **unproven** |
| `baseline-resolved`, `baseline-is-head`, `baseline-unresolvable` | `Baselines/BaselineResolverTests.Every_resolution_is_disclosed_with_the_sha_it_landed_on`, `A_baseline_resolving_to_HEAD_emits_its_own_notice`, `A_failed_resolution_is_disclosed_too` |
| `langversion-above-ceiling` | **unproven, and never emitted**: `ParseHealthTests.A_language_version_above_the_ceiling…` checks the constant's spelling and `ExceedsCeiling`, but nothing raises the notice |
| `selection-delivered-out-of-band` | **unproven**: emitted (`Renderer.cs:172,193`), never asserted |
| `test-runner-not-configured` | `Acceptance/AcceptanceTests.A_project_on_a_framework_needing_the_runner_setting_says_so`; acceptance row 11 |
| Reserved `data` keys mean the same thing everywhere | **unproven**: a convention with no check |

### Claims no test proves

This is the input to the next decision. Grouped by what kind of fix each needs.

**A. Documented, but Reach never does it.** The doc is wrong today, whichever way the owner goes.

1. `calli-unresolved` is never emitted.
2. `recompilation-widening` is never emitted. Acceptance row 18 expects it as a notice.
3. `langversion-above-ceiling` is never emitted.
4. README's `dotnet test --filter "$(…)"`: no command produces a bare filter.

**B. Reach does it, but no test checks it.**

5. Exit 70 on an internal error.
6. Exit 2 from a failed build, end to end.
7. Exit 0 on a `nothing-selected` run, end to end.
8. A report is written on exit 2, 4, 5 and 70.
9. `summary` is absent when the run failed before rendering.
10. `envelope.baseline` is absent on exit 4; `envelope.head` is absent when git cannot be read.
11. `packageVersion` is never emitted.
12. `runnerHost`: its value, absent on `skip`, present on run-all.
13. `delivery`: `inline` and `run-settings`, and absent on an entry with no command.
14. `willRun` is `0` on an unknown-total entry.
15. `ratio` is rounded to four places.
16. `no-changed-member-reached-a-test`, `all-changes-outside-analysis-scope` and
    `changes-were-formatting-only`, each emitted into `reasons` and not `notices`.
17. `selection-delivered-out-of-band`.
18. TeamCity is not detected.
19. `--artifacts-path` narrows discovery.
20. `.reach/` files survive the process exiting.
21. `changeSources`, `buildMode`, `forwardedBuildArguments`, `correspondence` as they appear in
    the report.

**C. Prose that drifts from what is pinned.**

22. The report-schema walk-through quotes values from `example-report.json` that no test
    compares.
23. The notice-code index claims to list every code with its kind, but nothing compares it to
    `NoticeCatalogue`. Item A shows it has already drifted.
24. The recipe's shell loop (failure propagation, stdin) is pinned as text but never executed.

**D. Claims about other people's tools.** Reach's suite cannot prove these, so they are a
different kind of choice: keep them as cited facts, or cut them.

25. `dnx` with a bare id fails on a prerelease, with that exact message.
26. `--ignore-exit-code 8` behaviour, and the .NET 11 SDK change; `--zero-tests-policy strict`.
27. `jq` is preinstalled on hosted runners.
28. The fetch-history setting for Azure DevOps, GitLab, Bitbucket, Jenkins and TeamCity.

### Found on the way

- **The acceptance layer's expected result has no `reasons` section.** [Ticket 03](03-capabilities-and-the-release-gate.md)
  widened it to will-run, notices, changes and tier. Rows 13 and 15 (and any
  `nothing-selected` row) can prove item 16 only if `reasons` joins that list.
- **`limitations.md` was outside this walk.** The README sends readers to it, and its
  register entries state a direction of failure for each gap. Whether those are claims in the
  gate is a scope question for the next ticket.
- **`select --help` disagrees with the schema.** It says `--paths` includes "the source path of
  every changed member"; the schema says it adds hop-by-hop paths. `--list-unselected` is in
  the help but not in the schema.
