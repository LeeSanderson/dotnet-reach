# Basics: known gaps and the known-gap guard

Status: ready-for-agent
Depends on: 03
Spec: [§4.5](../../acceptance-layer/spec.md#45-known-gaps), [§7.3](../../acceptance-layer/spec.md#73-known-gaps-not-in-the-gate) rows 24–25, [§2.2](../../acceptance-layer/spec.md#22-how-samples-are-split)

## Goal

Known-gap tests become declarable and visibly separate from the gate, and the two small gaps are
pinned.

## Scope

**The mechanism**:

- a `[KnownGap]` attribute that is an xUnit v3 trait, `Batch=known-gap`;
- a guard test (outside the acceptance namespace, so it is fast) that reflects over `Reach.Tests`
  and fails when a method named `Known_gap_…` lacks the trait, or a method with the trait is not
  named `Known_gap_…`, listing each offender.

**The rows**, on Basics, each with its own types:

| Row | Shape | Patch | Pins |
|---|---|---|---|
| 24 | `Handler<T>`, used as `Handler<Foo>` by some tests and `Handler<Bar>` by others | change code reached only through `Handler<Foo>` | the users of `Handler<Bar>` **are** selected (accepted over-selection, ADR-0006) |
| 25 | a type whose `ToString` is reached only through string interpolation | change that `ToString` | the interpolating test is **not** selected (documented under-selection) |

Name them `Known_gap_…`. Their `Because` names the register entry in `docs/limitations.md` that
documents the gap.

**List the gap-shaped members** (`Handler<T>` and its users, the interpolated type) in the Basics
README, so no gate patch touches them.

## Acceptance criteria

- Both known-gap tests pass, with exact expected selections, and carry the trait and the name.
- The guard test fails on a deliberately mismatched method (tested, for example, over a type built
  for the test) and passes on the suite.
- A run filtered to exclude `Batch=known-gap` runs every acceptance test except these two. Record
  the filter command in this ticket's comments. Ticket 16 uses it.

## Out of scope

Ledger's gaps (ticket 11).
