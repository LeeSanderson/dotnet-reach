# A props file inside a generator widens its consumers

Status: ready-for-agent
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
