# PRD amendments

Type: task (HITL)
Status: resolved
Blocked by: (none)

## Question

Charting produced decisions that contradict the approved PRD. The ADRs record the
reasoning, but PRD.md still states the superseded positions, and it is the document people
read first. It is marked "Approved v0.1", so amending it is the owner's call, not an
agent's.

The checklist for the owner:

- **§4.2, selection rules.** Rule 4, "It failed in the previous run", requires state from a
  previous run and is ruled out by ADR-0001. Remove it, or restate it as something a
  pipeline unions in itself.
- **§5, build handling.** The section concludes that freshness cannot be checked and
  documents `--no-build` as the caller's risk. ADR-0003 supersedes this: correspondence is
  verified in both modes via debug-symbol source checksums. The "known hazard" paragraph
  becomes a description of what the check defends against.
- **§8, analysis scope.** "The analysis scope is always every assembly in the solution" is
  a near-miss. ADR-0002 sharpens it to the union of test-project closures, which is what
  makes a single-test-project target legitimate. The warning about changed-side narrowing
  stands unchanged and should stay prominent.
- **§11, open questions.** The build-to-test ratio measurement has been consciously
  deferred rather than answered — record that, so it does not read as an outstanding gate
  that was quietly forgotten.
- **§12, M1 scope.** M1 as written says "the default and `--no-build` modes" and no more.
  It now also carries widening, edge provenance, three test frameworks with multiple
  dialects, and whole-project fallbacks. Restate it so the milestone matches the spec.

Added after [What is dotnet test --affected-tests](16-what-is-dotnet-test-affected-tests.md):

- **§9.1, coverage-based analysis.** The .NET team is building exactly the rejected
  approach — `dotnet test --affected-tests`, coverage instrumentation, a persisted map, a
  cached store keyed by commit, an expected cache-miss rate and a full-suite fallback leg.
  Cite it. §9.1's reasoning now has a worked example authored by the people building the
  alternative, which is worth more than the argument alone.
- **§11, risks.** Two honest additions. Coverage is *more precise exactly where Reach is
  weakest* — over-selection through interface dispatch in DI-heavy codebases, which a
  coverage map does not suffer from. And "no infrastructure ask" is a narrower moat than
  §9.1 assumes: once Microsoft's extension is published, adoption on Azure DevOps is a
  package reference, a `global.json` block and a cache task. The moat holds best for the
  secondary consultant audience, non-Azure CI, and the non-MTP majority. Record a trigger
  for revisiting: **the extension published publicly with a local filesystem provider**.

Added after [Generics, delegates and function
pointers](05-generics-delegates-and-function-pointers.md):

- **§9.2, compiled IL.** "IL exposes async state machines, lambdas and generic
  instantiations as concrete methods" is right about lambdas and misleading about the other
  two, and the gap between them is where two of M1's accepted holes come from. An async
  method's body is a concrete method, but nothing in first-party IL *calls* it — control
  reaches `MoveNext` through `AsyncTaskMethodBuilder.Start` inside the BCL — so the
  containment edge in [ADR-0006](../../docs/adr/0006-a-graph-node-is-an-il-method-definition.md)
  exists to reconnect it. And a generic instantiation is exposed at the *call site*, not as
  a distinct definition, which is why identity collapses instantiations. Restate the claim
  so it does not read as "this is free".
- **§8 and §12, the correctness rule.**
  [ADR-0008](../../docs/adr/0008-m1-accepts-named-under-selection.md) consciously overrides
  "under-selection is a correctness bug, not a tuning issue" for M1, in favour of named,
  reported holes. This is the single largest deviation from the approved PRD and the one
  most likely to be read as a bug. It needs the owner's explicit sign-off in the PRD text,
  not just in an ADR.

Added after [The report contract](07-the-report-contract.md):

- **§4.3, output contract.** The section describes "a filter suitable for `dotnet test
  --filter`, and a JSON report containing" five things. Both halves are now too small. Reach
  emits **complete argument vectors per (test project, target framework)**, not a filter
  string — because the correct rendering depends on framework generation, package version,
  runner host and which length escape hatch that host accepts, and because a selection past
  roughly 100 test methods does not fit on a command line at all (ADR-0009). And the report
  carries far more than five fields: outcomes, per-project modes, selection rules, path
  classes, a forward list of changes that reached nothing, notices with stable codes, a run
  envelope, and a versioned schema. Restate §4.3 as a pointer to the schema rather than an
  enumeration that will be wrong within a release, and say plainly that the schema is a
  product surface with a compatibility promise.
- **§4.2, selection rules.** Rule 3, "it is new since the baseline", is not free. Reach reads
  only the current compiled output and cannot enumerate the baseline's tests without building
  it, so newness has to be derived from the change set — and that derivation misses a test
  created by **adding `[Fact]` to an existing method**, whose body is byte-identical. Either
  §4.2 acknowledges that rule 3 depends on attribute-level change detection, or it names the
  hole. Escalated to [What counts as a changed member](18-what-counts-as-a-changed-member.md);
  the PRD should not keep listing the rule as though it were satisfied by a body hash.

Resolved when the owner has accepted or rejected each item and PRD.md reflects the outcome.
Record any rejection and its reasoning in the answer — a rejected amendment means the
corresponding ADR needs revisiting, not quietly ignoring.

## Answer

**Every item accepted. Nothing rejected, so no ADR needs revisiting.** PRD.md is now
**Approved v0.2 · 9 September 2026**, with a closing *Appendix — amendments since v0.1* that
tabulates each change against the decision record that drove it. That table, not this answer,
is the canonical index of what moved.

Three items were owner calls rather than transcription, and were decided as follows.

**§8/§12, ADR-0008 — accepted, conditional on the register.** The invariant stays the standing
rule; M1's override is a *bounded exception* in a new **§8.1**, and the bound is explicit: it
holds only while every accepted hole is in the limitations register and surfaced in the report
where a user judging a selection will see it. If the register lapses, the exception lapses and
the invariant governs again. §10's conservatism-plus-disclosure claim is cited as the reason
that bargain is honest rather than a loophole. M2 is stated as the return to an unexceptional
invariant, so there is no standing licence to under-select past M1.

This gave **§12 a second correctness criterion**: M1 cannot meet the shadow-mode gate, because
shadow mode is what measures it, so M1 is held instead to *every known hole registered and
reported, asserted by a test rather than by review*. That is the mechanical form of ADR-0008's
condition, and it is what [The limitations register](19-the-limitations-register.md) has to
satisfy. §3's definition of under-selection carries a pointer to §8.1 so the contradiction
cannot read as an oversight.

**§4.2 rule 4 — removed outright.** The alternative considered was restating it as something a
pipeline unions in for itself, which is safe (widening always is). Rejected in favour of a plain
removal citing ADR-0001: an invitation to union in a previously-failed set invites a consumer to
depend on behaviour Reach does not implement, test or report on.

**Recording — v0.2 with an amendment log.** Chosen over silent in-place editing because the
approved v0.1 has readers: anyone holding it can now see the delta and follow one link to the
reasoning. *How to read this document* also states that where the two disagree, the ADR wins.

Items handled by default, flagged rather than asked:

- **§4.2 rule 3** now says plainly that newness is *derived from the change set* — Reach cannot
  enumerate the baseline's tests without building it — and so depends on change detection seeing
  attribute-level edits, with the `[Fact]`-added-to-an-existing-method case named in §11 as open.
  Deliberately does **not** pre-empt [What counts as a changed
  member](18-what-counts-as-a-changed-member.md): the PRD records the dependency and the hole,
  and ticket 18 still owns the decision.

Four contradictions outside the checklist were found and fixed, all the same class of staleness
the ticket exists to clear:

- **§11, "Is standalone mode supported at all?"** — was still open, resting on freshness being
  uninferable. ADR-0003 removed the premise and both modes ship, so it is marked settled.
- **§11, data-driven test identity** — v0.1's *proposed* resolution is now adopted and confirmed
  safe by [Filter dialects and runner
  detection](01-filter-dialects-and-runner-detection.md).
- **§2, §4.1 step 6, §10** — all three still described the output as "a test filter", which §4.3
  no longer says. Now "the commands the pipeline should run".
- **§4.1 step 3** — "read every assembly in the solution's output" contradicted the new §8.2;
  it now points at the analysis scope.

Two additions worth noting as commitments, because both are now product surface rather than
design notes: **the report schema carries a compatibility promise** (additive growth, no
breaking changes), and **§11 records a revisit trigger** for the coverage argument — Microsoft's
extension published publicly with a local filesystem provider, at which point §9.1 is re-argued
from scratch rather than cited. A **§8 table row** was also added for a source-binary mismatch,
which ADR-0003 makes an error and the table did not list.

No new tickets. The documentation obligations this creates — a schema reference, the exit-code
table, a page per notice code — land in the fog patch that already holds them, and the register
is already [ticket 19](19-the-limitations-register.md).
