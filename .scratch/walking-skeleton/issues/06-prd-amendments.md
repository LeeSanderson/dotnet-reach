# PRD amendments

Type: task (HITL)
Status: open
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

Resolved when the owner has accepted or rejected each item and PRD.md reflects the outcome.
Record any rejection and its reasoning in the answer — a rejected amendment means the
corresponding ADR needs revisiting, not quietly ignoring.
