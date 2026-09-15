# Test recognition, the reverse walk and selection

Status: resolved
Depends on: 09, 10
Spec: [§12.1](../../walking-skeleton/spec.md#121-the-four-selection-rules), [§12.2](../../walking-skeleton/spec.md#122-test-recognition-is-data), [§13.3](../../walking-skeleton/spec.md#133-per-selected-test)

## Goal

Recognise test methods, walk backwards from the changed set to them, and record **why** each one
was selected and **how much to trust it**.

## Scope

**Test recognition is data, not code.** A table of framework, package, version range and
attribute names, so adding a framework touches no graph code. This is the one place the seam
genuinely varies — three frameworks, several dialects — and a table is cheaper than four
adapters.

M1 covers **xUnit (v2 and v3), NUnit and MSTest**, detected from the assembly's referenced
identities and versions in metadata. **Gate on package version as well as framework**: there is
**no xUnit v4** — the NuGet package `xunit.v3` is at *version* 4.0.0, where "v3" is the generation
— and 4.0.0 changed the filter surface, so ticket 13's dialect selection needs the version.

**An unrecognised framework falls back to whole-project selection**, with a
`whole-project-fallback` notice and a test total of **`unknown`, never `0`**. Reporting `0` would
silently corrupt ticket 18's ratio in exactly the case where Reach is running an entire project.

**The four selection rules.** A test method is selected if any hold:

1. **`reverse-reachable`** — reachable backwards along call edges from a change.
2. **`own-source-changed`** — its own declaration changed.
3. **`new-since-baseline`** — derived from the change set, not by comparing two test lists. Reach
   reads only the *current* compiled output and cannot enumerate the baseline's tests without
   building it. Ticket 06's declaration-level hashing is what makes this derivable at all: adding
   `[Fact]` to an existing method registers as a change.
4. **whole-project selection** — the project is unanalysable. **Deliberately not a member of the
   per-test `rules` array**: the entry's `mode: run-all` already says it, and a second way to say
   it is a second thing to keep consistent.

Each selected test records **which** rules selected it, because the rules are not interchangeable
and only the first is an analysis result. **A test selected only by rule 3 has no root and no
path class**, so an empty roots list is valid *exactly when* `reverse-reachable` is absent — which
is what makes a bug distinguishable from a fact.

**Selection granularity is the test method.** Individual cases of a parameterised test are never
selected independently. A method-level filter does match every case in all three frameworks —
established from adapter and framework source, not documentation.

**Path class: the weakest edge provenance on the *strongest* path.** A test reachable by any fully
compiled path reads `compiled` even if a widened path also exists; one reachable only through
widening reads `widened`. **This inverts naively taking the worst edge**, and it is the right
instrument: `widened` means "over-selection is plausible here", which is what the measurement
needs and the only class narrowing may ever touch. Compute it during the walk; a second traversal
is explicitly out of scope for M1.

**The change side is keyed on changes, not expanded roots.** Whole-assembly widening puts every
method of an assembly into the changed set, so one unattributable file expands into ten thousand
roots and every test in that assembly's dependents would carry all of them. A whole-assembly
widening contributes **one** root: *"assembly `Acme.Orders` widened, because `Acme.Orders.csproj`
could not be attributed to a member."* The expansion stays an implementation detail of the walk.

**The tier lives on the change, not on the (test, change) pair** — routing is a property of the
change; a pair inherits it by reference.

**Forward-indexed alongside it: every change, with its tier and the count of tests it reached.**
Affordable precisely because of the change-keying above — the list is diff-sized, not
graph-sized. **Counts only, never test names**; the pairs live once, on the test side. It is
arguably the most valuable field in the report: it makes *"I changed `OrderService.Submit` and
nothing runs"* a line you read rather than an absence you have to notice, which is either a
genuine coverage gap or an under-selection bug, and today both are invisible.

**Test method enumeration.** Every test method in every in-scope test project is enumerated, not
only the selected ones — the totals, the rendered-match computation (ticket 13) and the
measurement all read it.

**Hop-by-hop paths are opt-in** under `--paths`, reconstructed backwards over the reverse index.
They are the one genuinely unbounded thing in the document, which is why they are not on by
default.

## Acceptance criteria

- The recognition table detects xUnit v2, xUnit v3 (package version 4.0.0), NUnit and MSTest from
  referenced identities, asserted in memory.
- An unrecognised framework yields `mode: run-all`, `total: unknown` and
  `whole-project-fallback`.
- A change to a method two hops from a test selects that test with `rules: [reverse-reachable]`.
- A changed test method selects itself with both `own-source-changed` and, where applicable,
  `reverse-reachable`.
- A newly-added test method selects with `new-since-baseline` and an **empty** roots list, and
  that emptiness is distinguishable from a bug.
- **Path class: a test with both a compiled and a widened path reads `compiled`**, asserted
  directly. Getting this backwards is the easy mistake.
- A change reaching **no test at all** appears in the forward change list with a count of zero.
  **Named test** — it is the field that makes an under-selection visible, and only trustworthy if
  something proves it fires.
- Whole-assembly widening contributes one root, not thousands.
- `--paths` produces a hop sequence whose every hop names a kernel method.

## Out of scope

Widening edges themselves (ticket 14) — the walk must handle `widened` provenance correctly
before any widened edge exists, tested with hand-built edges. Rendering (ticket 13).

## Comments

**Implemented** in `Reach.Core/Selection`: `TestFrameworks` (the table), `TestMethods`
(enumeration), `ReverseWalk`, `RootSets`, `Selection` (the result types) and `Selector`.
`TestProjectRecognition` from ticket 04 stays as the *project*-level stub it always was —
whether a project declares tests at all, read from package references before anything is built.
The table here answers the different question of which framework a compiled assembly is written
against, and it is what everything downstream reads.

**Path class is two passes, not a priority queue.** The first follows only non-widened edges,
so everything it reaches is `compiled`; the second follows every edge starting from what the
first found, so everything newly reached is `widened`. That makes "the weakest provenance on
the *strongest* path" fall out rather than being computed, and it is the shape that makes
getting it backwards hard. The named test uses a graph where the widened path is *shorter*,
because that is the case where "first path found" and "worst edge seen" both give the wrong
answer.

**Synthesised edges count as compiled for path class.** Containment and type initialization are
invented by Reach, but the control flow they describe is not in doubt — they are as
non-removable as compiled edges, and `widened` has to keep meaning "over-selection is plausible
here" or the measurement reads nothing.

**A test that is itself a root of a change is not `reverse-reachable` by it.** Otherwise every
changed test would trivially be reverse-reachable from itself, `own-source-changed` would never
be the only rule, and the report's "empty roots list is valid exactly when reverse-reachable is
absent" would never hold. That is the rule that makes a bug distinguishable from a fact.

**One walk per change, not one walk with propagated root sets.** The changed set is diff-sized
and §16.3 forbids optimising ahead of a measurement, so `|changes| x O(V+E)` is the honest
implementation and it keeps the change-keying exact: a whole-assembly widening is one
`ChangeEntry` whatever it expands to.

**A derived test attribute still marks a test.** `[IntegrationFact] : FactAttribute` is
ordinary in real suites, and missing it would drop every test using it. Only first-party
derivations are followed — anything else needs the defining assembly, which is outside the
analysis scope — and the closure is iterated to a fixed point so a two-step derivation works.

**The fixture does not synthesise a test framework.** The real `xunit.v3.core` is already in
every in-memory compilation's reference set, because the suite itself runs on it, so a fixture
writing `[Xunit.Fact]` gets a genuine `xunit.v3.core` 4.0.0 assembly reference — exactly what
recognition reads. A fixture that writes no Xunit type gets no reference and falls back to
whole-project selection for the same reason a real project would. The first version compiled a
fake `xunit.v3.core` and every test failed with `CS0433`.

**Not built here, by design:** widened edges (ticket 14) do not exist yet, so the walk's
handling of `widened` provenance is asserted over hand-built graphs. That is the ticket's own
instruction, and it means the walk is correct before the first widened edge is produced.

One thing deferred to ticket 16: an unmappable path is currently routed straight to
whole-assembly widening for its containing project. That is the widening answer and therefore
safe, but the tier ladder and the rule table are what decide it properly.
