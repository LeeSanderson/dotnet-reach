# Changes outside the analysis scope

Status: ready-for-agent
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
