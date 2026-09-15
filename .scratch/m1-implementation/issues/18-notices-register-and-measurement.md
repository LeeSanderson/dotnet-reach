# The notice catalogue, the register parity test and the measurement

Status: resolved
Depends on: 12, 13, 16, 17
Spec: [§14.3](../../walking-skeleton/spec.md#143-notices), [§15](../../walking-skeleton/spec.md#15-the-over-selection-measurement) · [ADR-0008](../../../docs/adr/0008-m1-accepts-named-under-selection.md), [ADR-0012](../../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md)

## Goal

The mechanism that makes M1's whole bargain honest — **a build failure when a blind spot is not
written down** — plus the number that decides whether Reach is worth building further.

## Scope

### The notice catalogue

**A single global `notices` array, with locators. Never notices on entries as well.** An
unmodelled framework affecting three projects would otherwise be duplicated three times or split
across two channels, and a consumer would have to read both to be confident it had seen
everything. The `projects` locator gives the per-project view for free.

Each notice carries:

- **a stable kebab-case `code`, never renamed** — `unmapped-file-no-project`, not `REACH1042`.
  The numeric convention exists because compilers emit thousands of diagnostics needing terse bulk
  suppression; Reach will have dozens, they are **read** rather than suppressed, and a slug carries
  most of the explanation to the person staring at a surprising selection — and gives the register
  a natural anchor per hole instead of a lookup table.
- **one `kind`, and no severity axis**: `blind-spot` (may under-select) · `widening` (may
  over-select) · `scope` · `environment`. A `warning`/`info` axis alongside this could only ever
  disagree with it, and a pipeline that wants to gate gates on `blind-spot`.
- **a human `message` complete on its own**, so a consumer ignoring `data` loses structure but
  never meaning.
- **`data`, free-form per code.** The names `projects`, `assemblies`, `paths`, `members` are
  **reserved and used consistently** wherever they apply, which is what lets a consumer build
  "everything affecting this project" without knowing every code. **Do not type each code in the
  schema** — it would make every new code a breaking change, which fights the additive rule.

**Consolidate every code the earlier tickets emit into one catalogue**, including at minimum:
`unmapped-file-no-project`, `parse-failed`, `langversion-above-ceiling`, `nunit-filter-overmatch`,
`dialect-over-selects`, `recompilation-widening`, `signature-ambiguous`, `whole-project-fallback`,
`runsettings-filter-conflict`, `framework-selector-underivable`, `test-runner-not-configured`,
`ignored-untracked-assembly`, `calli-unresolved`, `untracked-source-files`,
`no-changed-member-reached-a-test`, `all-changes-outside-analysis-scope`,
`changes-were-formatting-only`, and the `environment` codes for baseline detection.

**Two codes this ticket adds outright:**

- **`framework-selector-underivable`** (`kind: widening`) — the project ran in full because Reach
  could not name its target frameworks on the command line (ticket 13).
- **`test-runner-not-configured`** (`kind: environment`) — an `xunit.v3` 4.0.0 project in a
  repository with **no `global.json` runner setting**, where `dotnet test` is a hard build error,
  so every invocation Reach emits is unrunnable and the error names nothing Reach-shaped. Both
  halves are free to detect: the MTP adapter is in the assembly's referenced identities, and
  `global.json` is a root file ticket 16's rule table already reads. **Reach still emits the
  invocations and does not stop** — the rendering is correct, the repository is misconfigured. The
  message prints the literal `global.json` block, the way exit 4 prints its line of YAML.

### The parity test — the whole mechanism

**Every notice code of kind `blind-spot` must have an entry in
[`docs/limitations.md`](../../../docs/limitations.md), and Reach's own test suite asserts the two
sets match exactly.** A new blind-spot code fails the build until it is registered.

That single test is what converts ADR-0008's "named and surfaced" clause from a promise into a
build failure, and it is why the register can be a hand-written document without drifting. It runs
**in memory over the notice catalogue** — no fixture needed. It is a test in the suite, not a CI
step: `dotnet test` runs it and CI needs no knowledge of it.

It also answers detectability from the clean side: **a gap Reach can detect *has* a code and is
mechanically tied to an entry; a gap it cannot detect has no code, and the document is the only
place it can live.** That is the argument for the document existing at all.

**Reconcile the register while you are here.** It currently holds twenty-three entries across
under-selection, over-selection, measurement and environment. Every code emitted by tickets 06–17
must either be a `blind-spot` with an entry, or a non-blind-spot code the schema reference will
index (ticket 20). Add entries for anything new; **do not remove entries** to make the test pass.

**No notice suppression in M1.** A suppression switch un-surfaces exactly the holes the bargain
depends on surfacing, and there is no evidence yet about which codes are noisy. Written into the
register while the reasoning is fresh: **if suppression ever ships, `blind-spot` must be
non-suppressible.**

### The measurement

**A field in the report, not a harness.** Every input is already carried for other reasons, so this
is arithmetic, not machinery.

- **Numerator: tests that will run**, not tests selected. The NUnit `~` rendering makes what runs a
  strict superset, and a deliberate dialect-level over-match *is* over-selection. **Both numbers
  are in the report and the gap between them is itself reported**, since it is the price of one
  named decision.
- **Denominator: enumerable test methods in in-scope test projects.** Unweighted — duration
  weighting is the more honest instrument and is unavailable, because Reach does not run tests and
  ADR-0001 forbids persisting anything between runs. Named as a **measurement limitation** rather
  than worked around.
- **`unknown` is reported as `unknown`.** A project on an unrecognised framework contributes to
  **neither** numerator nor denominator, and the summary carries two numbers:

  > `selected 312 of 1,840 tests (17%) · 2 projects on unrecognised frameworks run in full`

  No estimated denominator, no silent zero — treating an unenumerable project as zero would flatter
  the ratio in exactly the case where Reach is running an entire project, which is the one case
  where the number most needs to be ugly.
- **The widening delta is a count over (test, change) pairs whose path class is `widened`.** **No
  second walk in M1.** The rigorous version is genuinely different — a test with both a compiled
  and a widened path reads `compiled`, so the cheap number *understates* widening's contribution —
  and is registered as a measurement limitation with a second traversal as the upgrade path.

**No replay harness.** Replaying historical pull requests against each merge-base is the honest
version and is a separate tool; M1's job is to make every run emit the number so that a harness, or
a month of ordinary use, accumulates them for free.

**Record the stop condition in `docs/limitations.md` or the schema reference**, fixed before any
number exists, which is the only way it stays credible:

| Median PR selects | Verdict |
|---|---|
| under 40% | on target |
| 40–70% | works, but not sellable without framework models or narrowing; M2 justified |
| **over 70%, mostly `widened`** | **stop** — widening has eaten the value |
| over 70%, mostly `compiled` | **not Reach's failure** — the codebase is too connected for any test-impact analysis |

The last row is the one worth keeping, because it is the case most likely to be misread as a Reach
failure and to spend a quarter on models that cannot help.

## Acceptance criteria

- **The parity test exists, runs in memory, and fails when a `blind-spot` code has no register
  entry** — asserted by adding a throwaway code in the test and observing the failure.
- The reverse also holds: a register entry claiming a code that no longer exists fails.
- Every code in the catalogue has a `kind`, and the reserved `data` key names are used
  consistently where they apply.
- One global `notices` array; no notice is attached to an entry.
- `test-runner-not-configured` fires for an `xunit.v3` 4.0.0 project with no `global.json` runner
  setting, its message contains the literal `global.json` block, and the run **continues**.
- `framework-selector-underivable` fires for a multi-instance project carrying
  `TargetPlatformAttribute`.
- The measurement's numerator is the **rendered** count, asserted against a fixture where the two
  counts differ.
- An unenumerable project contributes to neither side, and the summary prints the second number.
- The widening delta counts `widened` pairs only, and a test reachable both ways is not counted.
- Notices sort deterministically by code then locator.

## Out of scope

Notice suppression. A second walk. A replay harness. Any generation of the register from the
catalogue, or of the catalogue from the register — the two carry different content, and generating
either from the other would mean putting explanatory prose in a JSON schema or run-time state in a
document. **The code is what binds them, plus this test.**

## Comments

**Implemented** as `Reach.Core/Reporting/NoticeCatalogue.cs` (code → kind, in one place),
`Reach.Core/Reporting/Measurement.cs`, `Reach.Core/Selection/TestRunnerConfiguration.cs`, and
`tests/Reach.Tests/Reporting/RegisterParityTests.cs`.

**The parity test found a real unregistered blind spot on its first run.**
`unresolved-first-party-member` had no entry, and now has one — direction under-selection,
detectable, with the reason it does not widen (it is a build problem, and correspondence is
what should have caught it). That is the mechanism working exactly as designed on the first
occasion it could.

**Two codes were renamed to match the register rather than the other way round.** The register
already said `recompilation-widening` and `untracked-source-files`; the code said
`recompilation-widened` and `untracked-source-in-change-set`. The register is the contract, so
the constants moved. A third test asserts every constant on `NoticeCodes` is in the catalogue,
so a code cannot be added without being classified.

**`Detectable: partially` counts as registered.** Several entries are partially detectable —
`parse-failed` and `calli-unresolved` among them — and they carry a code just as a fully
detectable one does. Reading only `yes` would have forced either a false claim in the register
or a missing entry.

**The measurement's numerator is `willRun`, not `selected`**, with both carried and the gap
between them reported. There is a named test over a fixture where the two differ, because
getting this the wrong way round would flatter the ratio by exactly the amount NUnit's `~`
rendering costs — and would be invisible.

**Dogfooding after this ticket found a second real bug**, and it is the more serious one.
`MetadataNames.WithoutInstantiation` cut at the *first* `<`, so a compiler-generated type name
that **starts** with one — `<>z__ReadOnlyArray\`1`, which is what a collection expression
compiles to — was truncated to the empty string. Every call into such a type resolved to
nothing and was silently lost. It surfaced as four `unresolved-first-party-member` notices with
an empty type name, which is precisely the kind of thing that notice exists to make visible.
Fixed by matching the closing bracket from the end, with its own test.

**The stop condition is in the register**, fixed before any number exists, with the row that
matters most kept: over 70% and mostly `compiled` is *not* Reach's failure, and is the case most
likely to cost a quarter on framework models that cannot help.

**The first real datapoint**, from running Reach on this repository at this commit:

```
selected 407 of 411 tests (99%) · widenedPairs 11
```

That number is not a verdict on the design. This working tree had changed `const` values in
`Reach.Core`, which every other assembly consumes — so the blast radius is exactly the one
recompilation widening exists to produce, and the forward change list names the declarations
that caused it. A meaningful median needs ordinary pull requests, which is what every run
emitting the number is for.
