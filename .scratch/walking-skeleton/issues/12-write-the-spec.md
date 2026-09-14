# Write the spec and implementation tickets

Type: task
Status: resolved
Blocked by: 20, 21, 22

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
- **Three resolved tickets carry amendments made after they closed**, and a spec drawn from
  their headline answers alone would be wrong. Read the `## Amended by ticket 22` sections of
  [the report contract](07-the-report-contract.md), [the fixture catalogue](11-fixture-catalogue.md)
  and [assembly discovery](14-assembly-discovery-under-ambiguous-output-layouts.md) alongside
  their answers.

Implementation tickets should be sliced so that each is one agent session, ordered so
something runs end to end early — the point of a walking skeleton is a thin path through
every architectural piece, not a complete component at a time.

Resolved when the spec exists and the implementation tickets are written and triaged.
Everything after that is a separate effort: this map is planning only.

## Answer

**[spec.md](../spec.md) exists, and twenty-two implementation tickets live in a separate
effort at [.scratch/m1-implementation/](../../m1-implementation/README.md).** The destination
is reached: nothing on this map is left to decide.

### The spec

Nineteen sections. §§1–17 are the specification; §18 names the implementation tickets and their
order; §19 is an index from every claim back to the one record that carries its reasoning, so a
reader can zoom rather than re-read the map.

Three reading rules are stated up front because they are what make the document usable without
the map. **CONTEXT.md is the vocabulary.** **Direction is stated for every rule** — *errs over*,
*errs under*, or *stops* — which is what ticket 12 asked for and is the spec's most repeated
structural element; an implementation ticket that does not know which direction is safe will pick
the wrong one under time pressure. And **where the ADRs and the spec disagree, the ADR wins and
the spec has a bug**, which keeps fifteen records authoritative rather than superseded by a
summary of themselves.

All four carry-forwards are discharged, and each is marked in the spec as a block quote so it
cannot be skimmed past:

- **The join stays abstract.** §11.8 fixes the identity a join must *produce* and what it must
  fan out, and explicitly does not choose a mechanism. Its ticket is a spike that builds both
  candidates, measures them on one fixture and **deletes the loser** — with the measurement
  table naming *failure direction* as the row that outranks the others, since a mechanism whose
  wrong answer is "no match, fall through to whole-assembly widening" is strictly preferable to
  one whose wrong answer is "matched the wrong method".
- **ADR-0003's checksum mechanism is unverified**, and its ticket's **first** acceptance
  criterion is verifying it against real build output before implementing anything, with an
  instruction to stop and report if it does not hold.
- **Every widening rule says which direction it errs and why.**
- **The three post-resolution amendments are honoured.** §19's index marks tickets 07, 11 and 14
  in bold as *read the `## Amended by ticket 22` section with the answer* — a spec drawn from
  their headline answers alone would have said an empty selection asserts exit code 0 in every
  host, that zero invocations implies `skip`, and that metadata always carries the target
  framework. All three are wrong.

Two things the spec adds that no single ticket owned. **§3's nine-phase pipeline** states the
invariant that makes the phase order legible rather than arbitrary: *no phase after correspondence
verification can fail the run* — from that point on, missing information widens and is disclosed.
And **§13.1 gives the report a concrete normative shape**; twenty-two tickets specified its fields
without anyone writing the document down, and an implementation ticket cannot be written against a
prose enumeration.

### The implementation tickets

Twenty-two, in a **separate effort directory**, and that separation is deliberate rather than
tidiness. Putting them in `.scratch/walking-skeleton/issues/` would have been the literal reading
of the tracker convention, and it would have poisoned the frontier: a future `/wayfinder` session
on this map scans that directory for open, unblocked, unclaimed tickets, and would have found
twenty-two build tickets and tried to resolve one as a decision. The effort slug is the axis the
tracker actually separates on, so `m1-implementation` gets its own.

Sliced into three stages, ordered so **something runs end to end early**:

- **A, the spine (01–13)** — at the end of it `dotnet reach select` runs end to end over
  **compiled edges only**, emitting `report.json` and runnable argv vectors. This is the walking
  skeleton in the strict sense: a thin path through every architectural piece, not a complete
  component at a time.
- **B, correctness (14–18)** — widening, synthesised edges, the tier ladder and rule table,
  recompilation widening and the parse rules, and the notice/register/measurement machinery.
- **C, acceptance and release (19–22)** — the fixture solution and its assertions, the four
  documents, publication, and Reach running on Reach.

**Stage ordering is integration order, not release order**, and the tickets say so: stage A alone
under-selects enormously, nothing is published until stage C, and the gate is the whole set.

Two ordering constraints are inherited rather than invented, and both are carried in the tickets
and the README: `ci.yml` is buildable from the first commit, and `dogfood.yml` plus the parity
test plus the fenced YAML block are one unit that arrives with `0.1.0-alpha.1` and cannot be
written earlier — you cannot dogfood before you publish.

**One ordering correction was made while writing.** The join was drafted at 09 and the call graph
at 10, then swapped: the join produces `MethodId`s, so identity and the per-assembly resolution
index must exist before either candidate can be measured. Numbering a dependency backwards in a
handoff artifact is exactly the kind of thing a fresh agent would trip over.

**Twenty-one are `ready-for-agent`; one is `ready-for-human`** — publishing, because creating the
NuGet.org registration, configuring Trusted Publishing and pushing the tag that makes an
irreversible publication cannot be delegated.

Each ticket carries its spec sections, its dependencies, an explicit **out of scope** block, and
acceptance criteria written as assertions rather than as descriptions. Where a decision is the
kind a competent engineer would reasonably reverse — MSBuild's base-name discovery rule, the
absorption test, narrowing recompilation widening to public surface, `.runsettings` as the
delivery default, newest-mtime disambiguation, rendering the native xUnit dialect — the ticket
says **do not re-add this** and gives the one-line reason, so the argument does not have to be had
again by someone who only has the code.

### What this does not do

It does not build anything. The map's Notes make it planning-only, and the pull to start coding
while drawing the spec together *is* the edge of the map. Stage A ticket 01 is where the next
session starts, in the other effort.
