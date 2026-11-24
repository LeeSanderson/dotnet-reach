# A props file inside a generator widens its consumers

Status: resolved
Depends on: 18, 16

## Goal

A `Directory.Build.props` (or `.targets`) inside a source generator's directory widens the
generator's consumers, as ticket 18 made every other change inside a generator do. That closes the
last under-selection of this shape.

## Background

Found by ticket 18. The owner decided on 2026-11-24 to fix it in this PR. Today such a file routes
by rule 2 (directory containment), which widens only the projects at or below it, which here is
the generator itself. The generator's assembly isn't in the graph, so nothing is rooted and nothing
is selected. Read ticket 18's `## Comments`, and
[the rule table](../../walking-skeleton/issues/15-the-unmappable-change-rule-table.md).

## Scope

1. When rule 2 would widen a project that Reach identifies as a generator (`TierLadder.IsGenerator`),
   it widens that generator's consumers as well. Don't add a new rule; extend where rule 2's
   projects are turned into roots. The same applies to any other rule that can name a generator as
   its widened project, so check them all.
2. A props file above both the generator and its consumers already widens them all. Don't change
   that.
3. Add a seam test (prior art: ticket 18's `Selection/GeneratorChangeTests` and the `TierLadderTests`
   theory). It should fail before the fix.
4. Update `docs/limitations.md` if it mentions this, and spec §9.2 if a claim gains a test.

## Acceptance criteria

- A props file inside a generator's directory selects the tests that reach its consumers.
- A props file that widens no generator behaves exactly as before.
- The full suite is green, and the Release `-warnaserror` build and format check are clean.

## Comments

- **Done.** `TierLadder.Route` now runs each rule and then adds the consumers of any generator
  the rule names (`WithGeneratorConsumers`, which uses rule 4's own `IsGenerator` and
  `ConsumersOf`). The rule and its reason are unchanged: a props file inside a generator still
  reports rule 2. The step only adds projects and never removes one, so the generator itself stays
  in the list.
- **Every rule checked.** Rule 2 was the only one that could name a generator without its
  consumers. Rule 1 on a generator's project file and rules 4–6 already route to the consumers.
  Rule 3 names every in-scope project, consumers included. Putting the step on `Route` covers them
  all, so a new rule gets it too.
- A props file above both the generator and its consumers widens the same projects as before
  (`A_Directory_Build_props_at_the_root_selects_every_project` is unchanged and passes).
- Seam tests, both seen to fail first: a `TierLadderTests` theory (`Directory.Build.props`,
  `.targets` and `.editorconfig` inside the generator select Billing and Generator, rule 2), and
  `GeneratorChangeTests.A_props_file_inside_a_generator_widens_its_consumers` (rooted through
  `RootSets.ChangesFrom`, roots are Core's methods).
- `docs/limitations.md` updated. Spec §9.2 has no claim this touches, so it is unchanged.
