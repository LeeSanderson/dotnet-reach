# Fixture catalogue

Type: grilling
Status: open
Blocked by: (none — 04, 05 resolved)

## Question

PRD §12 gives M1 the acceptance criterion "Correct on a sample repository" and nothing
more. This ticket turns that phrase into a list, because the fixtures *are* the
specification of correctness — a fixture that omits a shape proves nothing about it.

The charting session sketched the shapes a fixture solution must contain: interface
dispatch with two implementations; an abstract base with overrides; generics, including a
generic method used at two instantiations; `async`/`await` and an iterator; a lambda and a
local function; an explicit interface implementation; a multi-targeted project; a project
referenced with `ReferenceOutputAssembly=false`; a source generator project; a test project
in a framework Reach does not recognise, to prove the whole-project fallback fires; and a
change to a comment only, to prove it is *not* selected.

**To settle:**

- Is that list complete? The edge cases from
  [Change-set edge cases](04-change-set-edge-cases.md) and the node and edge definitions
  from [Generics, delegates and function pointers](05-generics-delegates-and-function-pointers.md)
  each imply fixtures that are not in it.
- One fixture solution containing everything, or several small ones? One is realistic and
  exercises scale; several are diagnosable when they fail.
- How does an integration test get a git history? A fixture committed in this repository
  has this repository's history, which is not a usable baseline. Constructing a temporary
  git repository per test is the obvious answer — confirm it, and decide how the fixture
  gets into it.
- What exactly is asserted: an exact expected selection, or that specific tests are in and
  specific tests are out? Exact assertions catch over-selection regressions and break on
  every fixture change.
- **The negative assertions matter most.** A test that must *not* be selected is the only
  kind that catches over-selection, and a test that *must* be selected is the only kind
  that catches under-selection. Both need to be explicit rather than emergent.
- Where does the boundary sit between in-memory compilation tests and fixture-solution
  integration tests? Anything testable in memory should be, since those run in
  milliseconds and the fixture builds do not.

**Required by the dialect research:** an NUnit parameterised test whose selection through
a rendered filter is asserted end to end. NUnit's VSTest `FullyQualifiedName` includes the
arguments, and method-level matching survives only via an adapter re-parse that several
supported configurations disable — so this is the one fixture standing between the design
and a silent under-selection. Ideally exercised under both the default configuration and
`UseNUnitFilter=false`.

## Comments

**From [The report contract](07-the-report-contract.md):** four fixtures, and one of them
changes the answer to "what exactly is asserted".

1. **Report determinism.** The report is specified as byte-deterministic for a given input,
   every array sorted by a documented key, with timings segregated into one envelope object
   so that excluding a single key makes two reports comparable. A fixture that runs the same
   input twice and compares the two documents pins this — and it is what makes exact
   assertions affordable elsewhere, since without determinism every assertion needs bespoke
   comparison logic. This bears directly on the open question above: **exact expected
   selections become the cheap option**, not the brittle one.
2. **An empty selection**, asserting the report emits **zero invocations** for the project —
   not an empty filter string, which runs everything. The dialect research already wanted an
   empty-selection fixture asserting exit code 0 in every host; this is the report-side half,
   and it is the assertion that stops the most expensive possible regression.
3. **A chunked selection.** Under `dotnet test` in VSTest mode there is no working response
   file, so a selection past the command-line ceiling splits across several invocations
   (ADR-0009). The ceiling arrives at roughly 100 test methods through `cmd`, so the fixture
   needs enough tests to cross it — assert that the invocations partition the selection with
   no test dropped and none duplicated.
4. **The NUnit `~` over-match count.** The existing NUnit fixture above proves the rendered
   filter does not *under*-select. This adds the other side: include a `MyTest` and a
   `MyTest2` where only `MyTest` is selected, and assert the report's rendered-match count
   reports `MyTest2` as an extra. That number feeds
   [the over-selection measurement](17-defining-the-over-selection-measurement.md), so a
   fixture that lets it silently read zero would corrupt the headline metric.

Also worth folding into the negative-assertion list above: a change that maps to a member
which reaches **no** test at all, asserting it appears in the report's forward change list
with a count of zero. That is the field that makes an under-selection visible, and it is
only trustworthy if something proves it fires.
