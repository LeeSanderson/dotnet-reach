# Changes outside the analysis scope

Status: resolved
Depends on: 07
Spec: [§7.4](../../acceptance-layer/spec.md#74-rows-added-by-this-spec) row 29

## Goal

When every change is outside the analysis scope, the report gives the reason
`all-changes-outside-analysis-scope`, as the docs promise. Row 29 asserts all three reason codes
end to end.

## Background

Found by ticket 07 (see its `## Comments`), and the owner decided to fix Reach in this PR on
2026-11-23 rather than cut the reason. `ReportBuilder.Reasons` gives
`all-changes-outside-analysis-scope` only when the selection's change list is empty. But
`RootSets` makes an entry for every member, widening and unmappable path, so an empty list means
an empty changed set, which is `no-changes`. A change in `Basics.Tools`, a project no test project
references, is listed (`DaysToKeep(int)`, count 0) and given the reason
`no-changed-member-reached-a-test`.

The M1 decision is in
[walking-skeleton ticket 04](../../walking-skeleton/issues/04-change-set-edge-cases.md): changes
outside the analysis scope are reported with a category and never selected on, and an empty
selection is never emitted without a reason list. The analysis scope is defined in ADR-0002.

## Scope

1. A change in a project outside the analysis scope is classified as outside the scope, wherever
   Reach already has that category. It isn't treated as a changed member that reached no test.
   Check the report schema for how such a change appears, and follow it.
2. When every change is outside the scope, the reason is `all-changes-outside-analysis-scope`.
   When some changes are in scope and reach nothing, the reason stays
   `no-changed-member-reached-a-test`.
3. Row 29 (`tools-only-change`) asserts `all-changes-outside-analysis-scope`. Between them, the
   Basics rows then assert all three reason codes end to end.
4. Add a unit test at `ReportBuilder` or at the scope classification seam, whichever changed.
5. Make sure `docs/report-schema.md` and the notice index describe what Reach now does.
   `NoticeIndexParityTests` from ticket 15 must stay green.

## Acceptance criteria

- Row 29 passes with `all-changes-outside-analysis-scope`.
- A run that mixes an out-of-scope change with an in-scope change that reaches nothing keeps
  `no-changed-member-reached-a-test`, and a test proves it.
- The full suite is green, and the Release `-warnaserror` build and format check are clean.

## Comments

- **Done.** A change whose file no in-scope project contains (nearest-ancestor containment, now
  one method, `AnalysisScope.ProjectContaining`, which `TierLadder` and `ChangedSetBuilder` share)
  keeps its entry in `changes`, with its tier and `testsReached: 0`, but its `reason` says it is
  outside the analysis scope and `ChangeEntry.OutsideAnalysisScope` is set. This covers unjoined
  members and type and assembly widenings (`RootSets`, three entry constructions wrapped; roots
  untouched, so no selection changes). `ReportBuilder.Reasons` gives
  `all-changes-outside-analysis-scope` when every change is so marked. Unit tests in
  `Reporting/ReportTests` (all outside; mixed with an in-scope change reaching nothing keeps
  `no-changed-member-reached-a-test`). Row 29 now asserts the new reason. `docs/report-schema.md`
  updated (the index rows and the `changes` section). Full suite 674/674, Release `-warnaserror`
  and format clean.
- **Not classified, deliberately:** an unmappable path. A file in no project (a README) keeps its
  `unmapped-file-no-project` category, so row 14 is unchanged. A project file of an out-of-scope
  project (`Basics.Tools.csproj`) routes by rule 1 to no project and still gets
  `changes-were-formatting-only`, which is wrong in the way ticket 07 noted for that reason.
- **Finding (Reach, possible under-selection): `RootSets.ProjectFor` is relative to the common
  root of the in-scope projects, not the repository root.** Changed paths are repository-relative,
  so when every in-scope project sits below one directory (all under `src/`, say), an unjoined
  in-scope member matches no project and roots nothing, and recompilation widening finds no
  declaring project. With a single in-scope project the opposite: every path matches it. Not
  fixed here, because fixing it changes selection. The scope classification uses the correct
  containment instead.
