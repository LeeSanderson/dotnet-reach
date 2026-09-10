# M1 resolves open design questions at 80/20

Where a design question has a simple answer that covers the common case and a complete
answer that covers every edge, M1 takes the simple one and writes the resulting constraint
down. The walking skeleton is a proof of concept: working code with stated limits is worth
more than an exhaustively-specified tool that does not exist, and coverage grows by closing
named constraints against real use rather than by anticipating every case before the first
run.

This is an owner decision about how the remaining design questions are answered, not about
what Reach does. It is recorded because its effects are visible in the artifacts — a rule
table with six rows where a ticket proposed twelve, four integration fixtures where the
catalogue listed twenty, no committed performance number — and a future reader finding them
would otherwise reasonably conclude the work was abandoned half-done. It was not; it was
scoped.

## The bargain

The simplification is paid for, not waived. Every one of them names its constraint in the
[limitations register](../../.scratch/walking-skeleton/issues/19-the-limitations-register.md),
with its direction of failure and whether Reach can detect an instance of it. That is what
separates this from cutting corners: the tool's own account of what it cannot do grows by
exactly the amount its implementation was simplified, so a user deciding whether to trust a
selection reads the same list the design was trimmed against.

Two directions, priced differently:

- A simplification that **over-selects** is free. It costs wasted test time and nothing else,
  and PRD §8's invariant is untouched. It needs a register entry only when the over-selection
  is large enough to affect adoption.
- A simplification that **under-selects** is a debt. It requires a register entry, and where
  Reach can detect an instance it requires a notice code, which
  [the report contract](../../.scratch/walking-skeleton/issues/07-the-report-contract.md)
  binds to a register entry with a test. Anything else is a silent hole, which
  [ADR-0008](0008-m1-accepts-named-under-selection.md) already forbids.

## Relationship to ADR-0008

[ADR-0008](0008-m1-accepts-named-under-selection.md) is the narrow case of this decision,
taken mid-ticket when it applied only to under-selection holes in the call graph. This
generalises it in two directions: across the whole remaining map rather than one ticket, and
across dimensions other than under-selection — the completeness of rule tables, the breadth
of the fixture catalogue, how many seams get interfaces, and whether M1 commits to a
performance number at all.

ADR-0008's reasoning carries over unchanged, including the part that binds it: PRD §10 scopes
the product claim to conservatism *plus disclosure*, so a named limit keeps the promise and a
silent one breaks it. PRD §8.1 already writes that exception in as bounded, and this decision
inherits the same bound. If the register lapses, both decisions lapse with it.

## What it does not license

It does not license deciding a question by leaving it undecided. A simplification is a
specific rule that a fresh engineer can implement and a test can assert; "handle the common
case" is not one. Each ticket resolved under this decision states the rule it adopted, the
case it does not cover, and the direction that case fails in.

Nor does it license simplifying the register itself, or notice suppression — the two
mechanisms the bargain runs on. Notice suppression is deliberately absent from M1 for that
reason.

## Revisit trigger

M2, which is where the accepted holes are closed and the §8 invariant governs without
exception. Sooner, if a real user reports an under-selection whose register entry existed and
did not help — that would be evidence the disclosure half of the bargain does not work in
practice, and the balance between simplicity and completeness would need re-arguing rather
than re-applying.
