# The join

Status: ready-for-agent
Depends on: 06, 07, 09
Spec: [§11.8](../../walking-skeleton/spec.md#118-the-join--deliberately-abstract), [§11.1](../../walking-skeleton/spec.md#111-a-node-is-an-il-method-definition)

## Goal

Turn a changed declaration in source into a `MethodId` in the call graph — by **measuring two
candidate mechanisms and picking one**, not by choosing on paper.

> **This ticket is a spike followed by an implementation, and the spec deliberately does not
> pre-empt it.** The design kept the **join** abstract from charting onward. Two mechanisms are
> live; both were judged plausible and neither was chosen, because the choice needs a
> measurement that only exists once there is code.

Ticket 09 lands first: the join produces `MethodId`s, so the identity representation and the
per-assembly resolution index must exist before either candidate can be measured.

## The spike

Build both, measure both on a fixture, write down the numbers, pick one, and **delete the
loser**.

**Candidate A — signature keys.** Derive a canonical signature from the Roslyn declaration
(fully-qualified declaring type, member name, generic arity, fully-qualified parameter types)
and look it up in the per-assembly resolution index ticket 10 already builds for cross-assembly
resolution.

**Candidate B — PDB line spans.** Take the changed declaration's source span from Roslyn and
find the method whose debug-symbol sequence points fall inside it. Ticket 08 already enumerates
every first-party document, so the symbol side is largely built.

**Measure, on the same fixture, for both:**

| Dimension | Why it decides |
|---|---|
| **Correctness on the hard shapes** | partial classes and partial methods; explicit interface implementations; overloads differing only in `ref`/`in`/`out` or by `modopt`/`modreq`; generic methods; nested and generic declaring types; local functions and lambdas; `file`-scoped types; records and their generated members |
| **Behaviour on a member with no IL** | an abstract or `extern` member, a `partial` declaration with no implementation |
| **Behaviour on a declaration the compiler did not emit** | trimmed by a conditional, or a phantom member from a misparse |
| **Cost** | time and allocations over the fixture solution, since the join runs once per changed member and the changed set is diff-sized |
| **Failure mode** | which direction does a *wrong* answer err in? |

The last row outranks the others. A mechanism whose failure mode is "no match, fall through to
whole-assembly widening" is far preferable to one whose failure mode is "matched the wrong
method", because the first over-selects and the second under-selects.

**Record the result in this ticket's Answer section** — the numbers, the choice, and what the
loser was worse at. A future reader will ask why one was picked.

## What the join owes, whichever mechanism wins

- **A changed property, indexer, event or operator declaration fans out to its accessors.**
  `get_Total`, `set_Total`, `op_Equality`, `add_Changed` are the nodes; the property is not.
- **An auto-property whose initializer changed maps to the constructor**, not to an accessor.
- **A field-like event's generated `add`/`remove` fan out the same way.**
- **A declaration that fails to join falls through to whole-assembly widening for its project.**
  Over-selection, and it is what makes a phantom member from a misparse survivable rather than
  a silent hole.
- **The report's `display` form is derivable from either candidate** — fully-qualified type,
  method name, generic arity, fully-qualified parameter types, assembly and TFM — so the report
  does not presuppose the outcome.
- **Roots render as kernel methods**, never `<Submit>b__0_1` or `MoveNext`. A change inside a
  state machine or lambda *is* a change to the kernel method, and a report naming
  compiler-generated members reads as a bug to everyone who sees it.

## Acceptance criteria

- Both candidates exist, are measured on the same fixture, and the numbers are in the Answer.
- One ships; the other is deleted, not left behind a flag.
- Every hard shape in the table above has a named in-memory test against the winner.
- A member with no IL, and a declaration the compiler did not emit, each fall through to
  whole-assembly widening rather than matching something else.
- A changed property selects tests reaching only its getter.
- An auto-property initializer change selects tests reaching the constructor.
- The seam is a function signature, not a C# `interface` — one adapter ships, so an interface
  would be indirection with one implementation forever, which is exactly what makes widening fan
  out in the codebase Reach will be pointed at.

## Out of scope

Changing the identity representation. Ticket 09 fixes the identity a join must *produce*; this
ticket decides how a declaration reaches it.
