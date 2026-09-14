# The fixture solution and the acceptance assertions

Status: ready-for-agent
Depends on: 13, 14, 15, 16, 17, 18
Spec: [§16.2](../../walking-skeleton/spec.md#162-testing-and-m1s-acceptance-criteria)

## Goal

**The fixtures are the specification of correctness** — a fixture that omits a shape proves
nothing about it. This ticket turns PRD §12's "correct on a sample repository" into a list, and
it is M1's acceptance gate.

## Scope

**Anything testable in memory is tested in memory.** In-memory Roslyn compilation runs in
milliseconds and can produce any IL shape on demand; a fixture solution has to build, and a
`dotnet build` is seconds at best. That boundary decides everything else in this ticket.

**One fixture solution.** It carries what is a *project-file fact* rather than an IL shape, and
needs to be realistic enough to exercise discovery at all: a multi-targeted project, a
`ReferenceOutputAssembly=false` reference, a source generator project, a test project per
recognised framework, and one on a framework Reach does not recognise.

**Git history: a temp repository per test.** Copy the fixture into a fresh temporary directory,
`git init`, commit as the baseline, then apply the change under test. A fixture committed in *this*
repository has this repository's history, which is not a usable baseline, and committing fixture
*history* would make every test depend on a real commit graph nobody can read. This also makes exit
4 testable — a shallow clone and a missing merge-base are two `git` commands away.

The runner needs `git` identity configured (`user.email`/`user.name` are unset on hosted runners
and `git commit` fails without them — ticket 01) and a NuGet restore of the fixture solution's own
packages.

### The four mandatory integration assertions

End to end **because each fails silently if it regresses** — no exception, no crash, just a wrong
answer that looks plausible. That is the criterion for earning an integration slot.

1. **The NUnit `~` filter, in both directions.** A parameterised test whose selection survives the
   rendered `FullyQualifiedName~` filter, under **both** the default configuration and
   `UseNUnitFilter=false`; *plus* a `MyTest`/`MyTest2` pair asserting the report's rendered-match
   count reports `MyTest2` as an extra. This is the one fixture standing between the design and a
   **silent under-selection**, and ticket 18's headline metric reads that second number — a fixture
   that let it silently return zero would corrupt the measurement.
2. **An empty selection.** Two halves:
   - **Report side**: `mode: skip` with `invocations: []`.
   - **Consumer side**, which is the half that earns the end-to-end slot: **a loop over
     `invocations` starts no process at all**, asserted by driving the loop and observing that no
     test command was issued.

   Two opposite failure modes are closed by the same fact: an empty filter string **runs
   everything**, and under Microsoft.Testing.Platform an empty-match filter **exits 8 and turns a
   green build red**. Note there is deliberately **no exit-code clause** here — with zero
   invocations there is no host and no exit code to assert.
3. **Whole-project fallback** on the unrecognised-framework test project: the project runs in full
   and the report says **`total: unknown`, not `0`**.
4. **Report determinism**: the same input twice, byte-identical once the timings envelope is
   excluded.

**Assertion 4 is what makes the rest cheap**, which is why it is mandatory despite proving nothing
about selection. With determinism pinned, **exact expected selections become the cheap option**
rather than the brittle one — so every selection assertion in this ticket is an **exact expected
selection**, not an in/out set.

### The negative assertions, each a named test

Emergent coverage does not count. A test that must *not* be selected is the only kind that catches
over-selection; a test that *must* be selected is the only kind that catches under-selection.

- **A comment-only change selects nothing.** Load-bearing now that the hashed surface is the whole
  declaration, so trivia-stripping has more to get right.
- **A `Directory.Build.props` under a subdirectory does not select projects outside it** — the
  assertion that proves directory containment is real.
- **A change reaching no test at all appears in the forward change list with a count of zero** —
  the field that makes an under-selection visible, and only trustworthy if something proves it
  fires.
- **A change reachable only through `Handler<Foo>` does select tests using only `Handler<Bar>`** —
  asserting the accepted over-selection from collapsing generic instantiations, so that decision
  stays a decision rather than drifting into a bug report.
- **A changed `const` consumed across an assembly boundary**, and **a removed overload rebinding an
  untouched call site** — both under-select if recompilation widening regresses.
- **`[Fact]` added to an existing method** — the cheapest test of the most expensive regression in
  the set.

### Layout tests are cheap now

Discovery scans rather than predicts, so a layout test **relocates already-built output** instead
of needing a project per layout. Include the **ambiguity error firing** when Debug and Release both
exist.

### What deliberately did not earn an integration slot

Do not promote these; each has a stated reason:

- **Chunking** — a unit test of the chunker. A hundred-test fixture solution to prove list
  partitioning is a bad trade.
- **The framework selector** — a unit assertion on the renderer. A missing selector fails
  **loudly** (exit 8, with the offending module named in the runner's own summary), so by this
  ticket's own criterion it does not qualify. The fixture solution's multi-targeted project already
  covers the report side through the exact-expected-selection assertions.
- **The blind-spot/register parity test** and the **docs↔workflow parity test** — both are tests in
  the suite, run by `dotnet test`, needing no fixture.
- **Every IL shape** — async, iterators, lambdas, `static readonly` delegate fields, local
  functions, explicit interface implementations, DIMs, static abstract members, accessors,
  sealed-type `using`, interpolation, constrained and unconstrained generic receivers, the stack
  merge. All in-memory, compiled from source strings;
  [`assets/il-subject`](../../walking-skeleton/assets/il-subject/README.md) is the starting corpus
  and maps each claim to the type that demonstrates it.

## Acceptance criteria

- All four integration assertions pass on Windows and Linux.
- Every negative assertion above exists as a **named** test, not an incidental one.
- Selection assertions are exact expected sets.
- The four integration tests are filterable as a group, so the fast suite stays fast in the single
  test project.
- The temp-repository helper is shared, not copy-pasted per test, and cleans up.
- A layout test relocates built output through at least three layouts, plus the ambiguity error.
- **The whole suite is green.** This ticket is M1's acceptance gate; nothing in stage C proceeds
  past a red one.

## Out of scope

Measuring over-selection on a real solution — smoke only here (the fixture solution and Reach's
own repository); real numbers come at adoption. A replay harness.
