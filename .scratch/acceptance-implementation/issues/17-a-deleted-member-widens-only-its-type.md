# A deleted member widens only its declaring type

Status: ready-for-agent
Depends on: 06
Spec: [§7.1](../../acceptance-layer/spec.md#71-release-gate-small-samples) row 5

## Goal

Deleting a member widens its declaring type, as M1 decided. It does not also widen the whole
assembly. Row 5 then selects only the tests that reach that type.

## Background

Found by ticket 06 (see its `## Comments`), and the owner decided to fix it in this PR on
2026-11-23. `DeclarationComparison` already adds the intended whole-type widening. The removed
member is also in `ChangedSet.Members`, though. It can't be matched to compiled code because it no
longer exists, so `RootSets.ChangesFrom` falls through to whole-assembly widening ("the declaration
could not be matched to compiled code"). The M1 rule is in
[walking-skeleton ticket 04](../../walking-skeleton/issues/04-change-set-edge-cases.md): a deleted
method widens its declaring type unconditionally.

## Scope

1. A removed member that the whole-type widening of its declaring type already covers no longer
   falls through to whole-assembly widening. A member that can't be matched for any other reason
   keeps today's fallback. That fallback is the safe direction, so don't narrow it.
2. ADR-0014 still holds. A removed member still widens the transitive referencers of its declaring
   assembly. This ticket removes only the extra widening of the declaring assembly itself.
3. Row 5 (`A_deleted_method_widens_at_least_its_declaring_type`) goes back to its intended
   expectation. It selects only the tests that reach `Readings`, worked out by hand. Rename the
   test if "at least" no longer fits. Delete `EveryBasicsTest` if row 5 was its only reason to
   exist. Row 12 uses it too, so keep it if row 12 still needs it.
4. Add an in-memory test at the seam that changed (prior art: `Selection/WholeProjectAndWideningTests`).
5. Update any doc or limitations entry that describes the old behaviour.

## Acceptance criteria

- Row 5 passes with its intended, hand-written selection and `Tiers`.
- An unjoinable member that is **not** a removal still widens its assembly, and a test proves it.
- The full suite is green, and the Release `-warnaserror` build and format check are clean.
