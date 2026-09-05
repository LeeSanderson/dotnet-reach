# Write the spec and implementation tickets

Type: task
Status: open
Blocked by: 04, 05, 06, 07, 08, 09, 10, 11, 13, 14, 15, 17

## Question

The destination. Produce `.scratch/walking-skeleton/spec.md` and the implementation
tickets, drawing the decisions together into something a fresh agent or engineer can build
from without rereading this map.

The spec covers: the pipeline end to end; the seams and what each one's contract is; the
correctness rules and which direction each errs in; the report schema; the CLI surface;
the fixture catalogue and acceptance criteria; and the performance budget.

**Carry these forward explicitly, because they are easy to lose:**

- The **join** stays abstract. The first implementation ticket for it is a spike that
  measures signature keys against debug-symbol line spans on a fixture and picks one. The
  spec must not pre-empt that.
- ADR-0003's PDB source-checksum mechanism **has not been verified against real build
  output**. Its implementation ticket carries that verification as an acceptance criterion,
  before the design is relied on.
- Every rule that errs toward widening must say so, and say why. PRD §8's invariant is the
  product; an implementation ticket that does not know which direction is safe will pick
  the wrong one under time pressure.

Implementation tickets should be sliced so that each is one agent session, ordered so
something runs end to end early — the point of a walking skeleton is a thin path through
every architectural piece, not a complete component at a time.

Resolved when the spec exists and the implementation tickets are written and triaged.
Everything after that is a separate effort: this map is planning only.
