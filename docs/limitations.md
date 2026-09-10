# Limitations register

What Reach cannot do, and which way each gap fails.

This document is load-bearing rather than documentation hygiene.
[ADR-0008](adr/0008-m1-accepts-named-under-selection.md) lets M1 ship with known
under-selection holes instead of widening for every one, and
[ADR-0012](adr/0012-m1-resolves-open-design-questions-at-80-20.md) generalises that to the
design as a whole — both **only** while every gap is written down here and surfaced in the
report where Reach can detect it. PRD §8.1 states the same bound: the exception lapses with the
register. So an unlisted limitation is not a documentation gap, it is a broken promise.

**How to read an entry.** *Direction* is the only field that matters at a glance:
`under-selection` means Reach may fail to run a test that would have caught a regression;
`over-selection` means it runs more than it needed to, which is wasteful and safe;
`measurement` means the number Reach reports about itself is imprecise.

**Detectable** entries carry a **notice code**, emitted in `report.json` when the run hit them.
Every code of kind `blind-spot` must appear here, and Reach's own test suite asserts the two
sets match exactly — so a new blind-spot code fails the build until it is registered.
Undetectable entries have no code and live only in this document, which is why the document
exists at all.

**Notices cannot be suppressed in M1.** A suppression switch un-surfaces exactly the gaps the
bargain above depends on surfacing, and there is no evidence yet about which codes are noisy in
practice. If suppression ever ships, `blind-spot` must be non-suppressible or the bargain is
void.

---

## Under-selection

The gaps that can cost you a regression. Listed worst first.

### First-party members invoked only from outside the analysis scope
- **Shape**: a first-party member that nothing in analysed IL calls, because the caller is the
  BCL or a framework. `$"{n}-{o}"` emits `DefaultInterpolatedStringHandler::AppendFormatted<T>`
  and never names `ToString`, so a test exercising an override only through interpolation
  cannot reach it. Generalises to `Equals`/`GetHashCode` via a dictionary, `CompareTo` via
  `Sort`, and every framework base-class override a host calls and first-party code never does —
  `BackgroundService.ExecuteAsync`, `DbContext.OnModelCreating`, `Controller.OnActionExecuting`.
- **Direction**: under-selection. **The largest hole in M1.**
- **Detectable**: no. There is no call site to infer from.
- **Upgrade path**: framework models (PRD §6), which is what they are for. The cheaper interim,
  designed and deliberately not adopted, is whole-type widening on the declaring type whenever a
  changed member overrides or implements a contract declared outside the analysis scope — it
  needs only the base-type walk and `MethodImpl` table widening already builds. See ADR-0008.
- **Revisit**: M2.

### C# newer than Reach's parser, misparsed silently
- **Shape**: Roslyn checks language versions at binding, not parsing, and Reach only ever calls
  `ParseText`. An older parser meeting newer syntax never throws; it can produce a clean tree
  that is structurally wrong with **zero diagnostics** — `record Person(string First)` on
  Roslyn 3.4 parses as a *method* named `Person` returning a type called `record`. Where the
  construct's members are swallowed from the tree, a change inside one never enters the changed
  set.
- **Direction**: under-selection.
- **Detectable**: partially. `parse-failed` fires on an error diagnostic or skipped-tokens
  trivia, and `langversion-above-ceiling` fires when a project declares a `LangVersion` above
  what Reach's Roslyn knows. Neither catches the silent case.
- **Mitigation in place**: parse with `LanguageVersion.Preview`, never `Latest`; ship the
  current Roslyn ([ADR-0013](adr/0013-the-tool-targets-net10-0.md)).
- **Upgrade path**: exercise the parser against the newest SDK in CI, so the gap is found by
  Reach's build rather than by a user.
- **Revisit**: each C# release.

### IL that changed with no source change and no rule-table trigger
- **Shape**: the compiler bakes decisions into consumers, so an assembly's IL can differ while
  its source is byte-identical. [ADR-0014](adr/0014-removals-and-constant-changes-widen-transitive-referencers.md)
  covers the two known triggers — removed members and changed compile-time constants — and the
  rule table covers SDK and project-file changes. The residue is any other cause, notably an
  agent's compiler version differing from the baseline's without a `global.json` change.
- **Direction**: under-selection.
- **Detectable**: no.
- **Upgrade path**: MVID comparison, which identifies every assembly whose IL changed whatever
  the cause. It needs the baseline's binaries, so it needs a second build;
  [ADR-0001](adr/0001-reach-persists-no-state-between-runs.md) rules out persisting them.
- **Revisit**: M2, alongside shadow mode, which would measure whether this ever fires.

### A changed file matching no rule and no project
- **Shape**: tier three's default row. A changed file that no debug symbols reference, that
  matches none of the rule table's rows, and that sits under no project directory, selects
  **nothing**.
- **Direction**: under-selection, and the only rule in Reach that deliberately errs this way.
- **Detectable**: **yes** — `unmapped-file-no-project`. Reach knows precisely when this fired.
- **Why**: the alternative is whole-solution selection for any unrecognised file, which means a
  README change runs the entire suite. Rows 1–5 of the rule table enumerate every
  build-affecting file that lives at a repository root, so the residue is documentation-shaped.
- **Upgrade path**: a configurable pattern list. Not in M1 because there is no evidence yet
  about what real repositories keep at their roots.

### Source generators and analysers Reach cannot identify
- **Shape**: generator detection is conventional, not prescribed — `OutputItemType="Analyzer"`
  on a `ProjectReference`, or a project setting `IsRoslynComponent` or
  `EnforceExtendedAnalyzerRules`. A generator arriving as a `PackageReference` or a bare
  `<Analyzer>` item is invisible, so a change to it does not widen its consumers.
- **Direction**: under-selection.
- **Detectable**: no — the failure is not knowing the generator exists.
- **Why not whole-suite on failure**: it would fire on every solution that has no generator at
  all, which is most of them.
- **Upgrade path**: reading the compilation's actual analyzer inputs, which needs MSBuild —
  rejected by PRD §9.2.

### Function pointers obtained outside analysed IL
- **Shape**: `Delegate.CreateDelegate`, `Marshal.GetFunctionPointerForDelegate`, native interop.
  Reach edges from the **capture** site (`ldftn`), so a pointer produced without one has no edge.
- **Direction**: under-selection.
- **Detectable**: partially — `calli-unresolved` fires on a `calli` with no inferable source.
- **Why no widening**: every method whose address is taken in analysed code already has its
  capture-site edge, so the only widening available is unbounded.

### Deserialization and reflective construction
- **Shape**: an object built by a deserializer or by reflection was never constructed by
  first-party IL, which defeats any argument leaning on the constructor being visible — including
  the whole-type widening upgrade path above.
- **Direction**: under-selection. **Detectable**: no. **Upgrade path**: framework models.

### Renames are never detected
- **Shape**: change detection is keyed on the declared type
  ([ADR-0005](adr/0005-change-detection-is-keyed-on-declared-type.md)), so a renamed type reads
  as one type deleted and another added. Deletion widens the declaring type, which covers most
  of it; a rename that also moves members can leave a gap.
- **Direction**: under-selection. **Detectable**: no.

### Ignored-and-untracked compiled files
- **Shape**: a source file that is both git-ignored and untracked is invisible to change
  detection but real to the compiler.
- **Direction**: under-selection.
- **Detectable**: **yes** — `ignored-untracked-assembly`. Reach can see the file; it cannot see
  its history, so it cannot decide whether it changed.

### Stale output under `--no-build`
- **Shape**: an assembly whose own sources are unchanged — so
  [ADR-0003](adr/0003-verify-source-binary-correspondence-via-pdb-checksums.md)'s checksums pass —
  but whose dependencies were rebuilt since. Cannot arise in default mode, which runs the build.
- **Direction**: under-selection. **Detectable**: no, without comparing dependency timestamps,
  which M1 does not do.

### The deferred absorption test for deleted methods
- **Shape**: a deleted method widens its declaring type unconditionally, because a deleted
  `override` or `operator ==` compiles clean and rebinds. An absorption test would prove most
  deletions inert and narrow this, and was written down with five clauses of under-selection risk
  rather than adopted.
- **Direction**: over-selection today; the *risk* is under-selection if the test is ever adopted
  carelessly. Registered so the analysis is not lost.

---

## Over-selection

Named, deliberate imprecision. Wasteful, never unsafe — but each is a cost worth knowing about,
and the first one distorts the numbers Reach reports about itself.

### The NUnit `~` filter over-match
- **Shape**: NUnit selections render as `FullyQualifiedName~Ns.C.MyTest` (contains, not
  equality), because NUnit's VSTest `FullyQualifiedName` includes a parameterised test's
  arguments and equality matching works only through an adapter re-parse that several supported
  configurations disable. So the emitted filter also matches `MyTest2`.
- **Direction**: over-selection. What runs is a strict superset of what was selected.
- **Detectable**: **yes** — `nunit-filter-overmatch`, and the report carries both the canonical
  count and the rendered filter's true match set.
- **Consequence**: the over-selection measurement's numerator is **tests that will run**, not
  tests selected.

### Recompilation widening's blast radius
- **Shape**: a removed member or changed compile-time constant widens every in-scope assembly
  transitively referencing the declaring one (ADR-0014). A `const` in a core assembly can widen
  most of a solution.
- **Direction**: over-selection. **Detectable**: **yes** — `recompilation-widening`, and the
  report's forward change list shows which change caused it.

### Generic instantiations are collapsed
- **Shape**: one node per generic definition, so a change reachable only through `Handler<Foo>`
  also selects tests using only `Handler<Bar>`
  ([ADR-0006](adr/0006-a-graph-node-is-an-il-method-definition.md)).
- **Direction**: over-selection. **Detectable**: no, and it needs no notice — it is a property of
  the graph, not an event in a run. Asserted by a fixture so it stays a decision.

### Widening residues from receiver-type inference
- **Shape**: three cases where [ADR-0007](adr/0007-widening-targets-the-inferred-receiver-type.md)
  cannot bound fan-out. An **interface-typed receiver** infers to the interface, which is correct
  and useless — and the DI shape *is* an interface-typed receiver, so **PRD §11's main technical
  risk is untouched**. A **stack merge** takes the common supertype, keeping full fan-out at that
  site. An **unconstrained generic receiver** falls to the slot-defining declaration.
- **Direction**: over-selection. **Detectable**: no.

### Static abstract interface members widen to every implementer
- **Shape**: the implementation is selected by the generic instantiation at the call site, which
  ADR-0006 discards, so nothing narrower is available.
- **Direction**: over-selection. **Detectable**: no.

### Ambiguous signatures edge to every candidate
- **Shape**: cross-assembly resolution keys on a canonical signature string; varargs,
  `modopt`/`modreq` and function-pointer parameters can leave genuine ambiguity.
- **Direction**: over-selection. **Detectable**: **yes** — `signature-ambiguous`.

### Whole-project selection on an unrecognised test framework
- **Shape**: a test project whose framework Reach does not recognise runs in full, and cannot be
  enumerated at all.
- **Direction**: over-selection. **Detectable**: **yes** — `whole-project-fallback`. The report
  reports its test total as `unknown` rather than `0`.

### A caller's `<TestCaseFilter>` downgrades widening to whole-project selection
- **Shape**: where the caller's runsettings already carry a test-case filter, Reach cannot
  compose with it safely, so a coarse-tier widening becomes whole-project selection
  ([ADR-0009](adr/0009-per-host-private-filter-channels-over-runsettings.md)).
- **Direction**: over-selection. **Detectable**: **yes** — `runsettings-filter-conflict`.

---

## Measurement

Limitations of the numbers Reach reports about itself. They do not affect selection.

### The over-selection ratio is unweighted by test duration
- **Shape**: selected tests as a fraction of all enumerable tests, counting each test equally. The
  honest instrument weights by duration, which flatters or damns the tool depending on where the
  slow tests sit.
- **Why**: Reach does not run tests, and ADR-0001 forbids persisting anything between runs, so
  there is no duration history to weight by.
- **Upgrade path**: read a test-run report the pipeline already produces. Out of scope for M1.

### The widening delta understates widening's contribution
- **Shape**: the delta is counted over (test, change) pairs whose path class is `widened`. A test
  reachable by *both* a compiled and a widened path reads `compiled`, so widening's true cost is
  higher than the number says.
- **Upgrade path**: run the walk twice, with widened edges and without — the rigorous version
  ADR-0004 exists to enable. Needs no new data, only a second traversal.

---

## Environment

### Reach does not work under default CI settings
- **Shape**: `actions/checkout` fetches one refspec at depth 1, so `merge-base` has neither the
  ref nor the history, and `refs/remotes/origin/HEAD` does not exist after *any* CI checkout
  because neither provider runs `git clone`
  ([ADR-0010](adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md)).
- **Direction**: not a selection gap — Reach stops. **Exit 4**, whose message prints the literal
  line of YAML that fixes it.
- **Detectable**: yes, and it is the likeliest outcome of anyone's first run.
