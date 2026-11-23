# A generator source change widens its consumers

Status: ready-for-agent
Depends on: 07, 17
Spec: [§7.1](../../acceptance-layer/spec.md#71-release-gate-small-samples) row 12

## Goal

Changing a source generator's `.cs` file widens the projects that consume the generator, exactly
as changing its project file does today. That closes an under-selection.

## Background

Found by ticket 07 (see its `## Comments`), and the owner decided to fix it in this PR on
2026-11-23. Rule 4 of the tier ladder (source generator → consumers) fires only on the
generator's project file. An edit to `Marker.cs` is listed as a changed member of the generator.
The generator's assembly isn't in the graph, so the change roots nothing and the report says
`nothing-selected`. With a real generator, that's an under-selection, which PRD §8 treats as a
correctness bug. It also contradicts CONTEXT.md's "unmappable change" and the rule table in
[walking-skeleton ticket 15](../../walking-skeleton/issues/15-the-unmappable-change-rule-table.md).

Ticket 17 changes the same fall-through in `RootSets.ChangesFrom`, so this ticket builds on it.

## Scope

1. Any change inside a project that Reach identifies as a generator routes to the
   generator-to-consumers rule. That includes a changed member, a new file and a deleted file. Use
   the same generator detection as rule 4 today. The tier is what rule 4 already reports.
2. Row 12 changes back to a `.cs` edit of the generator. Re-save its patch with `patch.cs save`,
   rename the patch and the test to match, and hand-write its expected result. Keep a `.csproj`
   row only if the spec's row list needs one; otherwise one row is enough.
3. Add an in-memory or rule-table test at the seam that changed (prior art: the tier-ladder and
   rule-table tests from `m1-implementation` ticket 16).
4. Update `docs/limitations.md`, `docs/report-schema.md` and the README wherever they describe
   generator changes.

## Acceptance criteria

- A generator `.cs` edit selects the tests that reach its consumers, both on Basics (row 12) and in
  memory.
- No change outside a generator project gets this routing.
- The full suite is green, and the Release `-warnaserror` build and format check are clean.
