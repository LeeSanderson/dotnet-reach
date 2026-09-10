# Fixture catalogue

Type: grilling
Status: resolved
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

## Answer

**One fixture solution, four mandatory integration assertions, everything else in memory.**
Resolved under [ADR-0012](../../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md).

### Where the line sits

The boundary question the ticket asks last turns out to decide all the others, so it goes
first: **anything testable in memory is tested in memory.** In-memory Roslyn compilation runs
in milliseconds and can produce any IL shape on demand; a fixture solution has to build, and a
`dotnet build` is seconds at best.

That leaves integration fixtures earning their place only where the thing under test *is* the
disk, the build, the git history, or a real test runner's behaviour. Every IL shape from
[ticket 05](05-generics-delegates-and-function-pointers.md) — async, iterators, lambdas,
`static readonly` delegate fields, local functions, explicit interface implementations, DIMs,
static abstract members, accessors, sealed-type `using`, interpolation, constrained and
unconstrained generic receivers, the stack merge — is an **in-memory** test compiled from a
source string. [assets/il-subject](../assets/il-subject/README.md) is the starting corpus and
was built for exactly this.

### The four integration assertions

These are end-to-end because each fails **silently** if it regresses — no exception, no
crash, just a wrong answer that looks plausible:

1. **The NUnit `~` filter, asserted in both directions.** The one fixture standing between the
   design and a silent under-selection. A parameterised test whose selection survives the
   rendered `FullyQualifiedName~` filter (under both the default configuration and
   `UseNUnitFilter=false`), *plus* a `MyTest`/`MyTest2` pair asserting the report's
   rendered-match count reports `MyTest2` as an extra. Ticket 17's headline metric reads that
   number, so a fixture that let it silently return zero would corrupt the measurement.
2. **An empty selection emits zero invocations**, not an empty filter string. An empty filter
   runs everything, which makes this the most expensive possible regression, and exit code 0
   in every host.
3. **Whole-project fallback** on a test project using an unrecognised framework, asserting the
   project runs in full and the report says `total: unknown` rather than `0`.
4. **Report determinism**: the same input twice, byte-identical once the timings envelope is
   excluded.

Assertion 4 is what makes the rest cheap, which is why it is mandatory despite proving nothing
about selection. With determinism pinned, **exact expected selections become the cheap option**
rather than the brittle one, so the answer to "exact selection or in/out sets" is **exact** —
resolving the open question the report contract's comment already anticipated.

### What did not make it end to end

- **Chunking past the command-line ceiling** becomes an in-memory unit test of the chunker:
  feed it 200 test identities, assert the invocations partition the set with nothing dropped
  or duplicated. A hundred-test fixture solution to prove list partitioning is a bad trade.
- **Multi-targeting, `ReferenceOutputAssembly=false` and a source generator project** stay in
  the one fixture solution as *structure*, since they are project-file facts rather than IL
  shapes, and the solution needs to be realistic to exercise discovery at all.
- **Output layouts** are cheap now: [ticket 14](14-assembly-discovery-under-ambiguous-output-layouts.md)
  replaced layout prediction with scan-and-verify, so a layout test **relocates already-built
  output** rather than needing a project per layout. Includes the ambiguity error firing when
  Debug and Release both exist.

### Git history: a temp repository per test

Confirmed as the ticket suspected, with the mechanism pinned: the fixture is **copied** into a
fresh temporary directory, `git init`, commit as the baseline, then apply the change under
test. A fixture committed in this repository has this repository's history, which is not a
usable baseline, and committing fixture *history* would make every test depend on a real commit
graph nobody can read.

This also makes [ADR-0010](../../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md)
testable — a shallow clone and a missing merge-base are two `git` commands away in a temp
repository, and exit 4 is the likeliest first run any real user has.

### The negative assertions, listed explicitly

The ticket is right that these matter most, and emergent coverage does not count. Each is a
named test:

- A **comment-only change** selects nothing. Now load-bearing rather than incidental:
  [ticket 18](18-what-counts-as-a-changed-member.md) widened the hashed surface from the body
  to the whole declaration, so trivia-stripping has more to get right.
- A `Directory.Build.props` under a subdirectory **does not** select projects outside it —
  the assertion that proves [ticket 15](15-the-unmappable-change-rule-table.md)'s directory
  containment is real.
- A change reaching **no test at all** appears in the forward change list with a count of zero.
- A change reachable only through `Handler<Foo>` **does** select tests using only
  `Handler<Bar>` — asserting the accepted over-selection from
  [ADR-0006](../../../docs/adr/0006-a-graph-node-is-an-il-method-definition.md), so that
  collapsing instantiations stays a decision rather than drifting.

### Three fixtures the newer tickets added

- A changed **`const` consumed across an assembly boundary**, and a **removed overload**
  rebinding an untouched call site — [ADR-0014](../../../docs/adr/0014-removals-and-constant-changes-widen-transitive-referencers.md)'s
  two triggers, both of which under-select if the rule regresses.
- **`[Fact]` added to an existing method**, from ticket 18. The cheapest test of the most
  expensive regression in the set.

### Consequences for other tickets

- **[Project layout and ports](10-project-layout-and-ports.md)**: one test project, and the
  in-memory-first split is what makes the metadata reader a parameter rather than a port.
- **[The limitations register](19-the-limitations-register.md)**: the register's
  every-`blind-spot`-code-has-an-entry test is an in-memory test over the notice catalogue, not
  a fixture.
- **[Write the spec](12-write-the-spec.md)**: the four integration assertions are M1's
  acceptance criteria, which is what PRD §12's "correct on a sample repository" turns into.
