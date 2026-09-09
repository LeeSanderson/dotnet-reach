# Defining the over-selection measurement

Type: grilling
Status: open
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
