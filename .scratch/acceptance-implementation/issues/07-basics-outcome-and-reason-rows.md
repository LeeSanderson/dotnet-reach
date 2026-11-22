# Basics: outcome and reason rows

Status: ready-for-agent
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
