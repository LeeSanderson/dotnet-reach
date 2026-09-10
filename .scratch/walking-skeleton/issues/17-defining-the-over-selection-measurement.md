# Defining the over-selection measurement

Type: grilling
Status: resolved
Blocked by: (none)

## Question

Graduated from the fog after
[What is dotnet test --affected-tests](16-what-is-dotnet-test-affected-tests.md), which
sharpened why it matters.

PRD §11 names over-selection through interface dispatch in DI-heavy codebases as the main
technical risk — possibly 80 to 90% of the suite, which would make the analysis pure
overhead. PRD §12 sets the target: *"median PR selects under 40% of the suite on a
representative solution"*. Neither says how the number is obtained.

Ticket 16 raised the stakes. Microsoft's coverage-based approach is **more precise exactly
where Reach is weakest**, because a coverage map records which implementation actually ran
and never has to widen through an interface. Reach's over-selection percentage is therefore
not a nice-to-have metric; it is the number that decides whether Reach is a product or an
interesting exercise, and it should be obtainable the day M1 first runs.

**This ticket does not run the measurement.** It decides what M1 must carry so that the
measurement is possible, and defines the measurement itself.

**To settle:**

- **What is measured.** Selected test methods as a fraction of all test methods, presumably
  — but a fraction of *what*: the whole solution, or only the test projects in scope?
  Weighted by historical test duration, or unweighted? Unweighted is easy and can flatter
  or damn the tool depending on where the slow tests sit.
- **Over what changes.** A single hand-made change proves nothing. Replaying historical
  pull requests against the repository at each merge-base is the honest version — decide
  whether M1 must support that, or whether it is a separate harness.
- **Against which solutions.** One open-source solution is a smoke target. PRD §11 wants
  two or three real ones and says results "may vary enormously between codebases".
- **The widening delta.** Edge provenance (ADR-0004) makes it possible to run the walk with
  and without widened edges. That difference *is* PRD §11's risk expressed as a number, and
  it separates "this codebase is highly connected" from "widening is costing us
  everything" — two very different conclusions with different responses.
- **Where the number is recorded**, so it is comparable across runs and codebases rather
  than being a figure someone remembers.
- **What M1 must carry to enable all of this** — most likely counts and provenance
  breakdowns in the report, and possibly a mode that runs the walk both ways. That part
  lands in the spec.

**And the honest question underneath:** what result would mean Reach should not be built
further? Deciding that *before* seeing the number is the only way the answer stays credible.

## Comments

**From [The report contract](07-the-report-contract.md):** three inputs are now available,
and one of them means the obvious numerator is wrong.

**The dialect over-match is over-selection and must be counted.** NUnit selections render as
`FullyQualifiedName~Ns.C.MyTest` (contains, not equality), so the emitted filter also matches
`MyTest2`. What actually runs is a strict superset of the canonical selection. The report
therefore carries **both** numbers — the canonical selected count, and the rendered filter's
true match set, computed by applying the dialect's matching semantics back over the enumerated
test list. Measuring only the canonical count would understate Reach's real over-selection by
whatever the `~` rendering adds, and on an NUnit-heavy solution that could be substantial. So
the measurement's numerator is **tests that will run**, not tests that were selected, and the
gap between the two is itself worth reporting since it is the price of one named decision.

**`total` can legitimately be `unknown`.** A test project on an unrecognised framework falls to
whole-project selection and cannot be enumerated at all. The report says `unknown` rather than
`0`, which means this ticket has to decide what such a project contributes to the
denominator — excluded, or counted at some estimate. Silently treating it as zero would flatter
the ratio in exactly the case where Reach is running the entire project.

**The widening delta is directly available.** Every (test, change) pair carries a **path
class**: the weakest edge provenance on the *strongest* path between them. A test whose every
path crosses a widened edge reads `widened`; one with any fully compiled path reads `compiled`.
So "how much of the selection exists only because of widening" is a count over pairs, not a
second walk with widened edges disabled — though a second walk remains the more rigorous
version and this ticket should decide whether the cheap number suffices.

## Answer

The measurement is **a field in the report, not a harness**. Resolved under
[ADR-0012](../../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md): every input
it needs is already being carried for other reasons, so M1 adds arithmetic rather than
machinery.

### What is measured

**Numerator: tests that will run.** Not tests selected. The report contract's comment settles
this and it is the whole reason the definition needed care — an NUnit selection renders as
`FullyQualifiedName~Ns.C.MyTest`, so `MyTest2` runs too. Measuring the canonical selection
would understate Reach's real cost by whatever the `~` rendering adds. Both numbers are in the
report; the measurement takes the rendered one, and **the gap between them is itself reported**,
since it is the price of one named decision.

**Denominator: enumerable test methods in in-scope test projects.** Unweighted. Duration
weighting is the more honest instrument — an unweighted ratio flatters or damns the tool
depending on where the slow tests sit — and it is not available: Reach does not run tests and
[ADR-0001](../../../docs/adr/0001-reach-persists-no-state-between-runs.md) forbids persisting
anything between runs, so there is no duration history to weight by. Named as a limitation of
the metric rather than worked around.

**`unknown` is reported as `unknown`.** A project on an unrecognised framework falls to
whole-project selection and cannot be enumerated at all, so it contributes to neither
numerator nor denominator. Instead the summary carries **two numbers**:

> `selected 312 of 1,840 tests (17%) · 2 projects on unrecognised frameworks run in full`

No estimated denominator, no silent zero. Treating an unenumerable project as zero would
flatter the ratio in exactly the case where Reach is running an entire project, which is the
one case where the number most needs to be ugly.

### The widening delta: the cheap number, and why it suffices

Every (test, change) pair already carries a path class, so *"how much of this selection exists
only because of widening"* is a count over pairs — the share whose class is `widened`. **No
second walk in M1.**

The rigorous version — run the walk with widened edges and again without, per
[ADR-0004](../../../docs/adr/0004-call-graph-edges-carry-provenance.md) — is a genuinely
different measurement, because a test with both a compiled and a widened path reads `compiled`
and so the cheap number *understates* widening's contribution. It is the upgrade path, not
M1's, and it is cheap to add later since it needs no new data, only a second traversal.

The cheap number is enough for the decision it informs, because it separates the two
conclusions the ticket correctly says are different:

- **Widened pairs dominate** → widening is costing everything, and framework models or
  narrowing would fix it. M2 is justified.
- **Compiled pairs dominate** → the codebase is simply highly connected. No selector helps,
  coverage-based or otherwise, and Reach is not the problem.

### Over what changes, and against which solutions

**No replay harness in M1.** Replaying historical pull requests against each merge-base is the
honest version and it is a separate tool; M1's job is to make every run emit the number, so
that a harness — or a month of ordinary use — accumulates them for free.

M1's own measurements are smoke, not evidence: **the fixture solution and Reach's own
repository**. Real numbers come at adoption, on a real solution, which keeps this consistent
with the owner's waiver of PRD §11's M0 measurement — both defer until something measurable
exists.

### Where the number is recorded

In the report, in a summary object beside the counts, so it is comparable across runs and
codebases without anyone remembering a figure. [Ticket 08](08-cli-surface.md) already put
over-selection as a percentage in the CLI summary, and this defines what that percentage is.

### What result would mean Reach should not be built further

Decided now, before any number exists, which is the only way the answer stays credible.
PRD §12's target is a median PR under 40%.

| Median PR selects | Verdict |
| --- | --- |
| under 40% | on target |
| 40–70% | works, but not sellable without framework models or narrowing; M2 is justified and M1 was worth building |
| **over 70%, mostly `widened`** | **stop.** Widening through interfaces has eaten the value, and coverage-based selection is the better answer for that codebase — PRD §9.1's honest form |
| over 70%, mostly `compiled` | not Reach's failure. The codebase is too connected for any test-impact analysis; report it and walk away |

The last row is the one worth keeping, because it is the case that would otherwise be
misread as a Reach failure and spend a quarter on models that cannot help.

### What M1 must carry

All of it is already committed elsewhere, which is the point: both selected counts (canonical
and rendered), a path class per (test, change) pair, per-project enumerability, and the summary
object above. No new mode, no second walk, no persisted history.

### Consequences for other tickets

- **[Fixture catalogue](11-fixture-catalogue.md)**: the `MyTest`/`MyTest2` fixture is what
  stops the rendered count silently reading zero and corrupting the headline metric. Already
  mandatory there.
- **[The limitations register](19-the-limitations-register.md)**: the metric is unweighted by
  duration, and the cheap widening delta understates widening's contribution. Both are
  limitations *of the measurement*, which is a category the register should hold as well.
- **[Write the spec](12-write-the-spec.md)**: the definition and the stop table.
