# A generator source change widens its consumers

Status: resolved
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

## Comments

- **Done.** Detection is rule 4's own (`TierLadder.IsGenerator`: some in-scope project references
  it with `OutputItemType="Analyzer"`), applied to the nearest ancestor project of the change's
  file. Two places use it:
  - `TierLadder.Route`: an unmappable path inside a generator (a file declaring no type, a
    template, any content) routes to rule 4, not rows 5–6.
  - `RootSets.ChangesFrom`: a member (joined or not), a type widening or an assembly widening
    (a deleted type or file, an unparseable file) whose file is in a generator keeps its entry,
    retiered `whole-assembly` with rule 4's reason, and its roots become its own roots **plus**
    every method of the consumers. The own roots are kept so a generator that a test project
    also references normally loses nothing.
  - Recompilation widening is untouched.
- Seam tests: `Selection/GeneratorChangeTests` (changed member, new type, deleted file, type
  widening, and a member outside a generator that is not routed) and a `TierLadderTests` theory
  for files inside a generator.
- Row 12 is now `A_change_to_a_generators_source_widens_its_consumers` over
  `generator-source-change.patch` (`Marker.Name`'s value); `generator-project-change.patch` is
  deleted, since the spec lists one row 12 and the `.csproj` case is covered by `TierLadderTests`.
  `Tiers` = `Basics.Generation.Marker.Name` `whole-assembly`. `docs/limitations.md` and the
  Basics README updated; `docs/report-schema.md` and the root README describe no generator rule.
- **Finding (docs vs Reach):** `docs/limitations.md` says detection also reads `IsRoslynComponent`
  and `EnforceExtendedAnalyzerRules`, but nothing in `src/` reads either: only the `Analyzer`
  reference marker is used. Left as is; out of this ticket's scope.
- **Merged with ticket 19.** Generator retiering wraps ticket 19's out-of-scope marking
  (`ThroughGenerator(new Change(Scoped(...), roots))`), and both now use
  `AnalysisScope.ProjectContaining`. They can't both fire. A generator is recognised only as an
  in-scope project that an in-scope project references as an analyser, so its changes are in scope
  and go to rule 4. A generator outside the scope has no in-scope consumer, so it can't reach a
  test. Its change is reported as outside the analysis scope, with the ticket 19 reason and
  `testsReached: 0`.
- **Not routed (by design of the ticket's scope):** a `Directory.Build.props` *inside* a generator's
  directory still goes to row 2, which widens the generator itself, so roots nothing. Rare, but
  the same under-selection shape; worth a follow-up if wanted.
