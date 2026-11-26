# Basics: outcome and reason rows

Status: resolved
Depends on: 03
Spec: [§2.3](../../acceptance-layer/spec.md#23-basics), [§5.2](../../acceptance-layer/spec.md#52-which-build-mode-reach-runs-in), [§7.1](../../acceptance-layer/spec.md#71-release-gate-small-samples) rows 12–16, [§7.4](../../acceptance-layer/spec.md#74-rows-added-by-this-spec) rows 28–29, [§8.1](../../acceptance-layer/spec.md#81-where-each-test-goes)

## Goal

Every outcome and every reason code, proven end to end on Basics: the generator row, the two
zero-outcomes, the zero-count change, the untracked file, the failed build and the out-of-scope
change.

## Scope

**Sample additions:**

- a source-generator project, consumed by `Basics.Core` with
  `ReferenceOutputAssembly="false" OutputItemType="Analyzer"`, as Contoso's is today;
- `Basics.Tools`, a production project no test project references (for row 29).

Both go in `Reach.slnx` under `/samples/` and build clean.

**Rows:**

| Row | Patch | Sections beyond the required four |
|---|---|---|
| 12 | change the generator | — . `Because` says it also proves the generator is not expected on disk |
| 13 | formatting only (whitespace, a comment) | `Notices`, `Reasons` = `changes-were-formatting-only` |
| 14 | a docs-only change (the sample's README) | — . Outcome `no-changes` |
| 15 | a member no test reaches | `Changes` (count 0), `Notices`, `Reasons` = `no-changed-member-reached-a-test` |
| 16 | a new `.cs` file in `Basics.Core` (the harness leaves it untracked) | `Notices` = `untracked-source-files` |
| 28 | a change that breaks the build | `Outcome` = `Failed`, `ExitCode` = 2 |
| 29 | a change only in `Basics.Tools` | `Notices`, `Reasons` = `all-changes-outside-analysis-scope` |

- **Rows 13, 15 and 29 give both `Notices` and `Reasons`**, so they prove the reason codes are in
  `reasons` and not in `notices`.
- **Row 28** depends on the harness's build-mode rule (spec §5.2). If ticket 02 did not implement
  that rule correctly, fix it here. Assert the outcome Reach reports, not the harness's own build
  output.
- **Row 29: pin what Reach does.** If `Basics.Tools` raises a standing notice on every Basics
  test, move it behind a baseline patch (spec §2.2) and record that in this ticket's comments. If
  Reach reports something other than `nothing-selected` with that reason, record it as a finding
  for the spec, and pin what Reach does.
- Row 12 must not fan out to every Basics test unless the generator really feeds every type. Keep
  its output small, so the selection reads clearly.

**Delete from `Acceptance/AcceptanceTests.cs`**, each with its replacement:
`An_empty_selection_is_skip_with_no_invocations_and_starts_no_process` (row 13 and the skip
invariant), `The_generator_project_is_in_scope_and_not_expected_on_disk` (row 12),
`A_change_reaching_no_test_appears_with_a_count_of_zero` (row 15).

## Acceptance criteria

- Seven new acceptance tests pass on Windows and Linux.
- Row 28 exits 2 with outcome `failed`, and a Basics test whose patch breaks the build by accident
  fails on `Outcome`, not on a harness assertion.
- All three reason codes are asserted, each with an exact `Notices` beside it.
- The three old tests are deleted. Every other Basics test still passes with the generator and
  `Basics.Tools` added. Neither added a notice to them.

## Out of scope

Rows 9–11 (ticket 08). The CLI-level exit-2 test (row 28 is the end-to-end proof; ticket 13 covers
the report-on-exit-2 claim).

## Comments

- **Done.** Basics gains `src/Basics.Generator` (a stand-in like Contoso's, `netstandard2.0`,
  consumed by `Basics.Core` with `ReferenceOutputAssembly="false" OutputItemType="Analyzer"`),
  `src/Basics.Tools` (`Housekeeping`, referenced by no test project) and `Basics.Core.Receipt` (no
  test reaches it; rows 13, 15 and 28 patch it). All three are in `Basics.slnx` and in `Reach.slnx`
  under `/samples/`. Neither project added a notice to any existing Basics test. Seven rows in
  `Acceptance/Basics/BasicsTests.cs`, patches saved with `tests/tools/patch.cs`:
  `generator-project-change`, `receipt-formatting-only`, `readme-only`, `receipt-header-change`,
  `voucher-new-file`, `receipt-breaks-the-build`, `tools-only-change`. The three old Contoso tests
  are deleted. The harness's §5.2 build-mode rule was already right: row 28 passes with `Failed`
  and exit 2 reported by Reach. Release `-warnaserror` build and `dotnet format --verify-no-changes`
  are clean.
- **Finding (spec, rows 13 and 14): the two outcomes are the other way round.** A formatting-only
  `.cs` edit leaves the changed set empty (no member, no widening, no unmappable path), so
  `ReportBuilder.OutcomeOf` says `no-changes`, with no reason. A README at the repository root is
  an unmappable path routed to no project, so it is `nothing-selected`, with
  `unmapped-file-no-project` and the reason `changes-were-formatting-only` (that reason fires on
  "no member changed, some path did", not on formatting). Pinned as Reach does it; the tests are
  named for what they prove (`A_formatting_only_change_is_no_changes`,
  `A_docs_only_change_selects_nothing_and_says_no_member_changed`).
- **Finding (spec and Reach, row 29): `all-changes-outside-analysis-scope` cannot fire end to end.**
  `ReportBuilder.Reasons` gives it when the selection's change list is empty, but `RootSets` makes
  an entry for every member, widening and unmappable path, so an empty list means an empty changed
  set, which is `no-changes`. The `Basics.Tools` change is listed (`DaysToKeep(int)`, count 0) and
  the reason is `no-changed-member-reached-a-test`. Pinned as Reach does it. So only two of the
  three reason codes are asserted end to end; the acceptance criterion cannot be met without a
  change to Reach or to the spec.
- **Finding (Reach, row 12): a change to a generator's `.cs` file selects nothing.** Rule 4 of the
  tier ladder fires only on the generator's project file. A `Marker.cs` edit is listed as a changed
  member of the generator (`Basics.Generation.Marker.Name`), whose assembly is not in the graph, so
  it roots nothing and the report is `nothing-selected`. With a real generator that is an under-selection, and it
  contradicts CONTEXT.md's "unmappable change". Row 12 therefore patches
  `Basics.Generator.csproj` (a harmless `<NoWarn>`), which widens `Basics.Core`.
- **Note for merges (row 12):** widening `Basics.Core` selects every Basics test that reaches it,
  so its `Selected` lists all six tests today. Ticket 06's new tests that reach `Basics.Core` must
  be added to it when the branches merge. This is the one row where §2.2's "unrelated code appears
  in no other test's expected result" cannot hold.
