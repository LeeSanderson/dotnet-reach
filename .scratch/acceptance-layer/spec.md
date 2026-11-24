# Reach's acceptance layer

**Status:** Specification · 22 November 2026
**Owner:** Lee Sanderson
**Derived from** the twelve resolved decision tickets of [the acceptance-layer map](map.md), the
charting decisions in its Decisions so far, and the solution-membership rule added by
[coding-standards](../coding-standards/spec.md). Amends [the M1 spec](../walking-skeleton/spec.md)
in two places (§11).

---

## 0. How to read this document

This is the buildable form of a design decided one question at a time. It draws the decisions
together so an agent can build the layer **without rereading the map**. Where a decision is
surprising, the spec states the rule and links the ticket that argues it. It does not argue it
again.

- **The vocabulary is the map's**, not `CONTEXT.md`'s. These are test-infrastructure terms:
  **sample solution**, **sample test**, **patch**, **baseline patch**, **change patch**,
  **acceptance test**, **known-gap test**, **release gate**. Each is defined in
  [the map's Notes](map.md#notes). Use them in identifiers and prose.
- **A row** means one numbered capability in §7. Rows 1–27 come from
  [ticket 03](issues/03-capabilities-and-the-release-gate.md). Rows 28 and 29 are added here (§7.4).
- **Where a decision ticket and this document disagree, the ticket's latest amendment wins**,
  and this document has a bug.

§§1–11 are the specification. §12 lists the implementation tickets. §13 is the index from each
claim back to its ticket.

---

## 1. What the layer is

### 1.1 Three jobs

The acceptance layer does three things at once:

1. **It is the release gate for `0.1.0-alpha.1`.** The gate is every claim the alpha's docs make,
   each proven by some suite (§9). The acceptance layer proves the claims about selection. Other
   suites prove the rest.
2. **It is a regression net.** It grows with each feature and each bug.
3. **It is a showcase** of what Reach handles, and of the gaps Reach names.

### 1.2 The oracle is an exact expected selection

Each acceptance test states by hand which sample tests Reach must select. **The sample tests are
never run to check Reach.** An exact match catches over-selection as well as under-selection, so
the tests that must *not* be selected are implied. They are never listed.

### 1.3 Standing rules

- **Reach's output is not changed to suit the layer.** Acceptance tests read `report.json` as
  Reach writes it. Where a test finds that Reach contradicts its docs, that is a finding for the
  docs or for Reach's design. It is not a reason to reshape the report.
- **Expected results are always written by hand.** They are never generated from Reach's output.
  There is no snapshot mode, no approval step and no pasteable literal in a failure message
  ([ticket 08](issues/08-writing-a-patch-and-keeping-it-applying.md)).
- **No old acceptance test is deleted before its replacement lands.** Each one goes in the same
  change as its replacement, or later. So none of M1's four silent-failure assertions is ever
  without a test ([ticket 06](issues/06-migrating-the-contoso-tests.md)).
- **The in-memory tests stay where they are.** About 25 of them assert change → selected tests.
  A sample solution may repeat their cases for the showcase. It does not replace them.

---

## 2. Sample solutions

### 2.1 Where they live, and the main solution

- **Each sample solution has its own folder at `tests/samples/<Name>/`**, with its own `.slnx`,
  a `README.md` and a `global.json`. That folder becomes the baseline repository Reach reads, so
  it holds the sample and nothing else. **No acceptance test lives inside it**
  ([ticket 04](issues/04-how-an-acceptance-test-is-declared.md)).
- **Every sample's projects are in `Reach.slnx` under the `/samples/` solution folder**, at
  wherever the sample lives. So CI's warnings-as-errors build and format check cover them. A sample
  is designed to meet the coding standard from the start.
- **Sample test projects are compiled by that build but never run by `dotnet test`.** CI's test
  steps target `tests/Reach.Tests/Reach.Tests.csproj` only. Sample tests run only through Reach,
  inside acceptance tests.
- **An acceptance test never runs Reach over the in-place tree.** The harness copies only
  git-tracked files (already true of `FixtureSolution` since coding-standards), so the in-place
  build's `bin/` and `obj/` never reach a test.
- `tests/fixtures/` is deleted once Contoso has moved (ticket 12 in §12).

### 2.2 How samples are split

**Small or multi-project is decided by what the patch proves.** If the point is that something
*is* selected, the test uses a small sample. If the point is that something is *not* selected, it
uses the multi-project sample, because only that one has tests to leave out
([ticket 03](issues/03-capabilities-and-the-release-gate.md)).

**Small samples are split by standing notice, not by area of capability**
([ticket 10](issues/10-the-small-sample-solutions.md)). Four notices fire on every run of a sample
that has their shape, whatever the patch:

| Notice | Shape that raises it |
|---|---|
| `nunit-filter-overmatch` | any NUnit test project |
| `whole-project-fallback` | any test project on an unrecognised framework |
| `framework-selector-underivable` | a multi-targeted project with a platform suffix |
| `test-runner-not-configured` | an `xunit.v3` 4.0.0 project with no runner setting in `global.json` |

A given `Notices` section is exact (§4.3), so a sample with one of these shapes makes every test
on it list that notice. **So a shape that brings a standing notice goes either on a sample whose
point it is, or behind a baseline patch (§3.3).**

Three further rules:

- **Every sample has `global.json` with the testing-platform runner setting**, so no test expects
  `test-runner-not-configured` except row 11. The file holds nothing else that changes how the
  sample builds (no SDK pin), so deleting it changes only the notice.
- **Each row's patch touches types that no other row's tests reach.** `Selected` lists only what is
  selected, so unrelated code in a sample appears in no other test's expected result. This matters
  most for row 4: its interface and virtual hierarchy must be its own types.
- **Each sample's README lists its gap-shaped members**, the ones known-gap tests depend on.
  Gate patches never touch them.

### 2.3 Basics

A small sample. It carries every small row except 7 and 8, and both small known gaps.

- one production project, `Basics.Core`, single `net10.0`;
- one `xunit.v3` 4.0.0 test project, `Basics.Tests`;
- a source-generator project, consumed by `Basics.Core` as an analyzer
  (`ReferenceOutputAssembly="false" OutputItemType="Analyzer"`), for row 12;
- one production project that no test project references, `Basics.Tools`, for row 29 (§7.4);
- `global.json` with the runner setting.

Rows 4, 24 and 25 are ordinary classes in `Basics.Core`, with their tests in `Basics.Tests`: an
interface and virtual hierarchy for row 4, a `Handler<T>` used as `Handler<Foo>` and
`Handler<Bar>` for row 24, and a type with an interpolated `ToString` for row 25.

### 2.4 Frameworks (was Contoso)

A small sample whose point is its standing notices. It carries rows 7 and 8 and the example
report.

- `tests/fixtures/Contoso/` moves to `tests/samples/Frameworks/` and **everything is renamed
  `Frameworks.*`**: solution, projects and namespaces.
- One production project, `Frameworks.Core`, **single `net10.0`** (its multi-targeting goes; rows
  9 and 10 are Basics baseline patches).
- Its **generator project goes** (row 12 is on Basics).
- Test projects are named by dialect: `Frameworks.Tests.XunitV2`, `Frameworks.Tests.XunitV3`
  (`xunit.v3` 3.x), `Frameworks.Tests.XunitV3_4` (`xunit.v3` 4.0.0), `Frameworks.Tests.NUnit`,
  `Frameworks.Tests.MSTest` and `Frameworks.Tests.Unrecognised`. Every one calls the same
  production code.
- The NUnit project keeps its parameterised test and the `MyTest`/`MyTest2` pair: `MyTest` reaches
  `Tax`, `MyTest2` reaches `Untouched`. Row 7's will-run count depends on that.
- The unrecognised project keeps its `Microsoft.NET.Test.Sdk` reference. That is what makes it a
  test project while its framework stays unrecognised.
- It gains `global.json` with the runner setting.

### 2.5 Ledger

The multi-project sample ([ticket 05](issues/05-the-first-multi-project-sample-solution.md)). A
small invoicing and collections back office: four production projects in a **diamond**, one test
project each.

```
src/Ledger.Domain                Invoice, Money, consts, IReminderChannel
src/Ledger.Billing               → Domain
src/Collections/Ledger.Dunning   → Domain   (under src/Collections/Directory.Build.props)
src/Ledger.Api                   → Billing, Dunning
tests/Ledger.Domain.Tests        → Domain
tests/Ledger.Billing.Tests       → Billing
tests/Ledger.Dunning.Tests       → Dunning
tests/Ledger.Api.Tests           → Api
```

- **Billing and Dunning are siblings.** Neither uses the other. That gives every row something to
  leave out.
- **All test projects are `xunit.v3`**, at a version that raises no notice. **Everything targets a
  single `net10.0`.**
- `tests/` stays flat. The `Collections` props covers `Ledger.Dunning` only, so row 21 shows
  containment *and* the reverse walk reaching a test project outside it.
- **Three sample tests per test project, twelve in all.** `Ledger.Api.Tests` is split on purpose:
  `Posting_an_invoice_returns_its_total` reaches Billing only;
  `Requesting_reminders_lists_the_overdue_invoices` reaches Dunning only; `Health_check_reports_ok`
  reaches neither. At least one Domain test touches `Invoice.DaysOverdue`, which only Dunning calls.
- **Gap-shaped members**: `IReminderChannel`, its implementers (`EmailChannel` in Dunning,
  `WebhookChannel` in Api, called through the interface by Dunning's `ReminderSender`) and
  `Money.DefaultCurrency`. No gate patch touches them.

**One multi-project sample is enough.** A later feature that needs a second one gets its own
ticket.

---

## 3. Patches

### 3.1 The format

**A patch is a plain unified diff, applied with `git apply`**
([ticket 01](issues/01-patch-format-and-readability.md)). The checked-in form has **only file
headers and hunks**:

```diff
--- a/src/Ledger.Domain/Invoice.cs
+++ b/src/Ledger.Domain/Invoice.cs
@@ -22,7 +22,8 @@ public sealed class Invoice(string customer, DateOnly due)
             return 0m;
         }
 
-        var monthsLate = daysLate / 30;
+        // A part month counts as a whole month.
+        var monthsLate = (daysLate + 29) / 30;
         return Total() * 0.015m * monthsLate;
     }
 }
```

- **No `diff --git`, `index`, or file-mode lines.** `git apply` applies the stripped form, and
  creates or deletes files from `/dev/null` alone.
- **The `---`/`+++` header stays.** Paths are relative to the sample's root, with `a/` and `b/`
  prefixes. `/dev/null` on one side means the file is created or deleted.
- **The `@@` hunk header stays, label included.** The label names the enclosing declaration.
- **The harness never uses `--3way`.** `git apply --3way --check` exits 0 on a patch that
  conflicts, which would hide drift.
- **A fast guard test rejects any `.patch` under `tests/Reach.Tests/Acceptance/`** that contains a
  `diff --git`, `index` or file-mode line. Its message names the tool's `save` command (§3.4).

### 3.2 Patches are independent

**Every patch applies cleanly to the untouched sample by itself.** Combining patches in
`Apply(...)` only stacks changes. Changes that depend on each other belong in one patch. This is a
rule of the spec, not a guard. There is no order to check.

### 3.3 Baseline patches

A **baseline patch** is applied to the copied sample **before** the baseline commit, so it is part
of the baseline and Reach never sees it as a change. A **change patch** is applied after the
commit. A baseline patch gives a sample a variant starting state without a directory per variant,
and without the variant's edit becoming a second claim in the expected result
([ticket 10](issues/10-the-small-sample-solutions.md)).

- **At most one baseline patch per acceptance test.**
- Independence holds per **(baseline patch, change patch) pair**: a change patch is written against
  the untouched sample or against exactly one baseline patch.
- Baseline patches sit flat beside the change patches.

### 3.4 Writing a patch: `tests/tools/patch.cs`

A single-file C# app **outside `Reach.slnx`**
([ticket 08](issues/08-writing-a-patch-and-keeping-it-applying.md)):

```
dotnet run tests/tools/patch.cs -- save  <Sample> <name> [--on <baseline>] [--force]
dotnet run tests/tools/patch.cs -- apply <Sample> <name> [--on <baseline>]
```

`<Sample>` is the folder name under `tests/samples/`. `<name>` and `<baseline>` are written
without `.patch`.

- **`save`** takes the uncommitted edits to `tests/samples/<Sample>/` and writes
  `tests/Reach.Tests/Acceptance/<Sample>/<name>.patch` in the stripped form. Then it reverts the
  sample, **only after the write succeeds**, so nothing is lost. It refuses to overwrite an
  existing patch without `--force`. With `--on <baseline>`, the edits are diffed against the sample
  with that baseline patch applied, and the revert restores the untouched sample.
- **`apply`** applies an existing patch to the sample in the working tree with
  `git apply --3way`, for fixing drift or starting a new patch from an old one. `--3way` is fine
  here because a human checks the result, though on a stripped patch it can only fall back to a
  plain apply (§3.5). With `--on <baseline>`, it applies the baseline patch first.

### 3.5 Drift

A failed apply in the harness reports, on top of git's file and line:

1. which patch failed, by name, and its role: `baseline: no-global-json.patch`, or
   `2 of 3: dunning-reminder.patch`;
2. git's full `git apply --verbose` output, which shows the context it searched for;
3. one fix line: ``dotnet run tests/tools/patch.cs -- apply <Sample> <name> [--on <baseline>]``,
   then `save --force`.

**There is no separate check that patches apply.** Drift shows when the layer runs, which CI does
on every push. The routine is `apply`, then `save --force`, then re-read the `Because` of every
test that names the patch. **`apply` cannot merge.** A stripped patch has no `index` lines, so
`--3way` has no blobs to merge from and git falls back to a plain apply. Drift that git absorbs by
offset or fuzz applies, and `save --force` then rewrites the patch at its new place. Drift it
cannot absorb fails outright, with no conflict markers. Then the developer makes the change again
by hand and runs `save --force`. The tool says so when it happens.

---

## 4. Declaring an acceptance test

### 4.1 Code, not data

**An acceptance test is one C# test method** ([ticket 04](issues/04-how-an-acceptance-test-is-declared.md)).
Tests share patches, pieces of expected results and harness steps as ordinary C#.

- **One folder per sample at `tests/Reach.Tests/Acceptance/<Name>/`**, holding one test class and
  that sample's `.patch` files, flat. The class names its sample once. Today's
  `Acceptance/AcceptanceTests.cs` is replaced; `ExampleReportTests.cs` stays.
- The namespace `Reach.Tests.Acceptance` still selects the whole group.
- Patches are read from the source tree, not copied to the build output.

### 4.2 How a test reads

```csharp
[Fact]
public async Task A_late_fee_change_selects_dunning_but_not_billing() =>
    await Apply("late-fee-counts-part-months.patch").ExpectAsync(new()
    {
        Because = "Only Invoice.DaysOverdue changed; Dunning reaches it, Billing doesn't.",
        Outcome = Outcome.Selected,
        Selected = new()
        {
            ["Ledger.Domain.Tests"] = ["Ledger.Domain.Tests.InvoiceTests.Overdue_counts_calendar_days"],
            ["Ledger.Dunning.Tests"] = ["Ledger.Dunning.Tests.ReminderTests.A_reminder_names_the_days_overdue"],
        },
        RunInFull = [],
        Tiers = new() { ["Ledger.Domain.Invoice.DaysOverdue(DateOnly)"] = Tier.Member },
    });

[Fact]
public async Task A_missing_runner_setting_is_reported() =>
    await Baseline("no-global-json.patch").Apply("core-body-change.patch").ExpectAsync(new() { ... });
```

**The method is `ExpectAsync`**, not `Expect`: Sonar S4261 requires the suffix on an async method,
and the name was changed rather than the rule suppressed (implementation ticket 02). This document
says `Expect` for the step in prose.

- **`Apply(...)` names its change patches explicitly**: one or more, relative to the sample's
  acceptance folder. A missing patch, or one `git apply` rejects, fails the test as §3.5 says.
- **`Baseline(...)` names at most one baseline patch**, and comes before `Apply(...)`. The exact
  builder spelling is the implementer's, provided a second baseline patch cannot be expressed.
- **Shared expected-result pieces are static members** (for example, Ledger rows 20 and 22 share
  one).

### 4.3 The `Expected` record

| Member | Required | Meaning |
|---|---|---|
| `Because` | yes | One sentence: why exactly these tests. Printed first in every failure. |
| `Outcome` | yes | `report.json`'s outcome: `Selected` · `NothingSelected` · `NoChanges` · `Failed`. |
| `Selected` | yes | Test-project key → full `display` names of the selected tests. A project not listed has nothing selected. |
| `RunInFull` | yes | Test-project keys run in full by whole-project fallback. |
| `ExitCode` | when `Outcome` is `Failed` | The exit code the failed run must return. Checked at run time: `Expect` fails if it is missing for `Failed`, or given for any other outcome. |
| `WillRun` | no | Test-project key → the rendered filter's true match count, as the schema has it. |
| `Notices` | no | The set of notice codes. |
| `Reasons` | no | The set of reason codes (`nothing-selected` only). |
| `Changes` | no | Member `display` → `testsReached`. |
| `Tiers` | no | Member `display` → tier. |

- **An optional section that is given is exact.** A notice nobody expected fails the test. Left
  out means not checked.
- **Every report carries `baseline-resolved` and `baseline-is-head`.** The harness passes the
  baseline SHA as `--base` and leaves change patches uncommitted, so `HEAD` is the baseline. A
  given `Notices` section lists both, through a shared static piece (`EveryRunNotices` on Basics).
- **Keys.** Test-project name is the `.csproj` file name without extension. A multi-targeted
  project's entries are written **`Name@tfm`**. A bare name matching more than one `report.json`
  entry is an ambiguity error, so a framework cannot be skipped by accident.
- Chained calls (`.Selects(...).RunsInFull(...)`) were rejected: "required" would become a
  convention, not something the compiler checks.

### 4.4 What `Expect` does

1. Runs the harness (§5) and Reach twice.
2. Compares **every** given section and reports **all** the differences, section by section, with
   `Because` at the top. It never prints a pasteable `Expected` literal.
3. Checks the invariants that hold for every report, whatever the patch:
   - **Determinism**: the two reports are identical after scrubbing (§5.4).
   - **Exit code matches outcome**: `Selected`, `NothingSelected` and `NoChanges` exit 0; `Failed`
     exits `Expected.ExitCode` ([ticket 12](issues/12-test-it-or-cut-it.md)).
   - **Skip means nothing runs**: a project with nothing selected that does not run in full has
     mode `skip` and **zero invocations**.
   - **Unknown is never zero**: a total Reach did not count serialises `"unknown"`, and a project
     run in full never serialises a total of `0`
     ([ticket 06](issues/06-migrating-the-contoso-tests.md)). A project run in full may carry a
     total it counted: a platform-suffixed project (row 10) is analysed and only rendered
     project-wide. Narrowed to this intent, with the owner's approval, by implementation ticket 08.

None of these is ever written in a test.

### 4.5 Known gaps

A known-gap test pins a deliberate over-selection, or a documented under-selection, with an exact
expected result. It turns red when the gap closes. **Known gaps are never in the release gate.**

- It carries **both** a `[KnownGap]` attribute (an xUnit v3 trait, `Batch=known-gap`) **and** a
  method name starting `Known_gap_`. The name is for the human reader. The trait is what the gate
  excludes.
- **A guard test fails where the two disagree**: a `Known_gap_` method without the trait, or the
  trait on a method not named `Known_gap_`.

---

## 5. The harness

### 5.1 A fresh temp root per test

Each acceptance test gets its own temp root, deleted when the test ends
([ticket 09](issues/09-how-the-harness-builds-each-acceptance-test.md)). Nothing is shared, so no
test's result depends on which test ran before it.

```
<temp root>/
  repo/   the copied sample and the patch result, nothing else
  out/    report.json files, logs
```

Reach treats untracked files under its root as changes, so nothing the harness writes may land in
`repo/`.

1. Copy the sample's **git-tracked files** into `repo/`.
2. If the test names a baseline patch, `git apply` it.
3. Write `.gitignore` (`bin/`, `obj/`), `git init`, and commit everything as `baseline`.
4. `git apply --verbose` each change patch, in order, **left uncommitted**.
5. Assert that `git status --porcelain` lists only the paths the change patches touched.
6. `dotnet build` the sample solution.
7. Run Reach in-process (`ReachPipeline.SelectAsync`) with `--base <baseline sha>`, twice, each
   writing to `out/`.

Reach's change set covers committed, staged and unstaged changes alike, so uncommitted patches
give the same result as a PR commit. One accepted difference: a patch that adds a file leaves it
untracked, which Reach always includes.

### 5.2 Which build mode Reach runs in

**If step 6 succeeds, Reach runs with `NoBuild = true` both times**, as decided (one build per
test).

**If step 6 fails, the harness does not fail the test.** It runs Reach in **default build mode**,
twice, so Reach invokes the build, sees it fail and reports the failure itself. This is how row 28
(a patch that breaks the build) proves exit 2 end to end, with no extra harness API. A row whose
patch breaks the build by accident fails on its required `Outcome` and `ExitCode` instead.

Step 6's build output goes to `out/`, and a failure message includes its tail.

### 5.3 Parallelism

**`Reach.Tests` runs with `ParallelMode.All`** (`xunit.v3` 4.0.0), with `maxParallelThreads` left
at its default. Methods of one class run in parallel. Anything that cannot run in parallel opts
out with `DisableParallelization`.

This corrects [ticket 02](issues/02-building-a-patched-sample-cheaply.md)'s "xUnit v3 runs one
class's tests one at a time". The first implementation step runs the whole suite under `All` and
marks whatever breaks. **If more than a handful of tests break, fall back to `Collections`** and
record the per-sample floor (Ledger serial ≈ 1 minute).

**Sample builds share a machine-wide limit** (`BuildSlots`), added on 2026-11-24 after parallel
agent runs exhausted a developer machine's memory. Uncapped, every acceptance test builds at once
and each build starts one MSBuild node per processor, so the process count grows with the square
of the core count, times every concurrent test run. Steps 6 and 7 run only while the test holds a
slot. A slot is an exclusive lock on a file under `%TEMP%/reach-build-slots/`, so test runs in
different worktrees share the limit, and the OS frees a slot when its process dies. There are
half as many slots as processors, between 1 and 4 (`REACH_BUILD_SLOTS` overrides that). Each build
gets `-maxCpuCount` of processors ÷ slots. MSBuild node reuse stays on: with concurrency bounded,
nodes are reused, and a build that has to start cold nodes takes three times as long.

### 5.4 Determinism

Two Reach runs are never byte-identical as written: `timings` differs every run, as
`docs/report-schema.md` says. So an **in-house scrubber** removes `timings` and replaces the temp
root with a fixed token. The two results are compared as strings, and a mismatch prints a diff.
**No Verify (VerifyTests) dependency.** Its snapshot mode would reverse §1.3.

The second run costs about 1 s against a 3.5–5.5 s warm build.

### 5.5 `FixtureSolution` becomes `SampleCopy`

`FixtureSolution` already copies, makes the temp git repository, commits, builds and runs Reach
in-process. It is **reworked, not deleted**: it takes a sample name, applies patches, and is renamed
(for example `SampleCopy`). Its stale class-fixture comment goes. Its old API
(`WriteFile`/`Touch`/`Rebuild`) goes when the last Contoso test has moved.

### 5.6 The named upgrade

**Persistent slots between runs** (for example `%TEMP%/reach-acceptance/<Sample>/<n>`, reset with
`git reset --hard` and `git clean -fd`, keeping the ignored `bin/` and `obj/`) would make a
developer's second run incremental. They cost staleness detection and hermeticity. **Trigger: the
CI acceptance step takes more than 5 minutes on either OS.** `Apply(...)` hides the build shape,
so the switch changes no test. Not built now.

---

## 6. Where the acceptance tests run

From [ticket 07](issues/07-where-acceptance-tests-run.md).

- **They stay in `Reach.Tests`**, in the `Reach.Tests.Acceptance` namespace.
- **Plain `dotnet test` runs everything**, acceptance tests included. There is no blessed fast-suite
  command and no opt-in flag. A developer whose change has a small blast radius narrows the run with
  an ordinary filter, for example `dotnet test -- --filter-namespace "Reach.Tests.Graph*"`. The
  contributor docs say so.
- **CI runs them on every push to `main` and every pull request, on Windows and Linux**, as a
  second step in the existing matrix job, sharing its one build:
  1. `dotnet test --project tests/Reach.Tests/Reach.Tests.csproj ... -- --filter-not-namespace "Reach.Tests.Acceptance*"`
  2. `dotnet test --project tests/Reach.Tests/Reach.Tests.csproj ... -- --filter-namespace "Reach.Tests.Acceptance*"`

  A failing step names the failing layer.
- **No time budget.** The acceptance step gets `timeout-minutes: 20`, with a comment saying it
  guards against a hung build and is not a budget.

---

## 7. The rows

Each row is one acceptance test unless it says otherwise. **The patch's subject is a suggestion.**
The claim, the sample and the expected sections are the spec. Where a row says *pin what Reach
does*, the implementer confirms the outcome against Reach. If Reach contradicts the docs, that is a
finding to report, not a change to Reach's output (§1.3).

### 7.1 Release gate, small samples

| # | Capability | Sample | Sections beyond the required four |
|---|---|---|---|
| 1 | A change selects exactly the tests that reach it through compiled edges | Basics | — |
| 2 | A test whose own body changed is selected | Basics | — |
| 3 | An existing non-test method, body unchanged, that gains `[Fact]` is selected | Basics | — |
| 4 | Interface or virtual dispatch selects every candidate implementation | Basics | — |
| 5 | A deleted method widens its declaring type | Basics | `Tiers` (`whole-type`) |
| 6 | A move to another directory, same namespace, widens that type | Basics | `Tiers` (`whole-type`) |
| 7 | Each dialect is recognised and renders its own filter; NUnit's `~` runs more than it selects | Frameworks | `WillRun`, `Notices` |
| 8 | A test project on an unrecognised framework runs in full, with an unknown total | Frameworks | `Notices` |
| 9 | A multi-targeted test project gives one entry per framework, each pinned with `-f` | Basics † | keys `Basics.Tests@net8.0`, `@net10.0` |
| 10 | A platform-suffixed framework makes its project run in full | Basics † | `Notices` |
| 11 | `xunit.v3` 4.0.0 without the runner setting gets its notice | Basics † | `Notices` |
| 12 | A generator project is in scope, and a change to it widens its consumers | Basics | — |
| 13 | A formatting-only change gives `no-changes` | Basics | `Notices`, `Reasons` (none) |
| 14 | A docs-only change gives `nothing-selected` | Basics | `Notices` (`unmapped-file-no-project`), `Reasons` (`changes-were-formatting-only`) |
| 15 | A changed member that reaches no test is listed with a count of 0 | Basics | `Changes`, `Notices`, `Reasons` (`no-changed-member-reached-a-test`) |
| 16 | A new C# file not tracked by git is flagged | Basics | `Notices` |

Notes on specific rows:

- **Row 3** is the case walking-skeleton ticket 07 escalated. A brand-new test method is not tested
  separately: it is a strict superset (new declaration and new body).
- **Row 7's** patch is the `Tax` change, and it is **the same patch file** `ExampleReportTests`
  uses (§8.2).
- **Row 12's** `Because` says why it also proves the generator is not expected on disk: if it were,
  every Basics test would end `failed`.
- **Rows 14, 15 and 29** give `Notices` as well as `Reasons`, so they prove the reason codes go in
  `reasons` and not in `notices`.
- **Rows 13 and 14 were the other way round** in the first draft, and implementation ticket 07
  found it. A formatting-only `.cs` edit leaves the changed set empty, so it is `no-changes` with
  no reason. A README is an unmappable path routed to no project, so it is `nothing-selected`,
  flagged `unmapped-file-no-project`, with the reason `changes-were-formatting-only`. That reason
  fires whenever no member changed but some path did, whatever its name says. The docs were
  corrected to match Reach (implementation ticket 16).
- **Row 12** changes a generator's `.cs` source. Implementation ticket 18 made Reach widen a
  generator's consumers for any change inside it, not only its project file.

† Via a baseline patch:

- **Row 9**: retarget `Basics.Core` **and** `Basics.Tests` to `net8.0;net10.0` (a `net8.0` test
  project cannot reference a `net10.0`-only library). The change patch is a body change.
- **Row 10**: retarget the same two projects to `net10.0;net10.0-windows` with
  `<EnableWindowsTargeting>true</EnableWindowsTargeting>`. Without that setting the Linux leg fails
  with `NETSDK1100`.
- **Row 11**: delete `global.json`. The change patch is any one-line body change.

### 7.2 Release gate, the multi-project sample

Each of these is about what is **not** selected. All on Ledger.

| # | Patch | Selected | **Not selected** | Extra sections |
|---|---|---|---|---|
| 17 | Body of `Invoice.DaysOverdue` (Domain), called only by Dunning | its Domain test, the Dunning tests reaching it, Api's reminders test | Billing.Tests, the other Domain tests, Api's posting and health tests | — |
| 18 | A `const` in Billing (`TaxRules.StandardRatePercent`) | **all** Billing.Tests, **all** Api.Tests | Domain.Tests, Dunning.Tests | `Tiers`, `Notices` (`recompilation-widening`) |
| 19 | Remove an unused overload in Dunning (`ReminderSchedule.Next(DateOnly)`) | all Dunning.Tests, all Api.Tests | Domain.Tests, Billing.Tests | `Tiers`, `Notices` (`recompilation-widening`) |
| 20 | `Ledger.Billing.csproj` (a harmless `<NoWarn>`) | Billing.Tests, Api's posting test | Domain, Dunning, Api's reminders and health tests | — |
| 21 | `src/Collections/Directory.Build.props` | Dunning.Tests, Api's reminders test | Domain, Billing, Api's posting and health tests | — |
| 22 | Move a Billing type to another namespace | as row 20 | as row 20 | `Tiers` (`whole-assembly`) |
| 23 | A root file that matches no rule (`CODEOWNERS`) | nothing | everything | `Notices` |

- Rows 18 and 19 select **all** of `Ledger.Api.Tests`, health test included, because that project
  itself transitively references the declaring assembly and is widened whole. Rows 20 and 22 do
  not. The contrast is worth showing.
- **Row 18 contradicts this table today, and is open for the owner** (implementation ticket 16).
  It also selects `Ledger.Dunning.Tests.ReminderSenderTests`. Api is widened whole, and the reverse
  walk from `WebhookChannel.Send` comes back into Dunning through `ReminderSender`'s
  `IReminderChannel.Send` call: row 26's interface fan-out reaching into row 18. The test pins
  what Reach does.
- **Row 22's `Tiers` include a field at `whole-assembly`.** A field with no initialiser has no
  compiled body to join, so it falls back to whole-assembly widening. It widens the assembly the
  moved type already widened, so the selection is unchanged. `docs/limitations.md` registers the
  fall-back ("A changed declaration with no compiled body widens its whole assembly").
- **Row 23: pin what Reach does.** `ReportBuilder.OutcomeOf` returns `no-changes` when the change
  set is empty, so `CODEOWNERS` may report `no-changes` rather than `nothing-selected` with
  `unmapped-file-no-project`. The docs state row 23 as intended behaviour, so a contradiction is a
  finding.

### 7.3 Known gaps, not in the gate

| # | Gap | Sample |
|---|---|---|
| 24 | Generic instantiations are collapsed: a `Handler<Foo>` change also selects the users of `Handler<Bar>` | Basics |
| 25 | An interpolated `ToString` is not selected | Basics |
| 26 | An interface-typed receiver fans out to every implementer: an `EmailChannel.Send` change still selects Api's reminders test, which uses `WebhookChannel` | Ledger |
| 27 | Recompilation widening's blast radius: a `Money.DefaultCurrency` change selects all 12 tests. Gives `Changes`, to show the one `const` that did it | Ledger |

### 7.4 Rows added by this spec

[Ticket 12](issues/12-test-it-or-cut-it.md) decided two things that need a row it did not
number. Both are Basics, release gate.

| # | Capability | Sections beyond the required four |
|---|---|---|
| 28 | A change that breaks the build gives outcome `failed` and exit 2 | `ExitCode` = 2 |
| 29 | A change only to a project no test project reaches gives `nothing-selected` | `Notices`, `Reasons` (`all-changes-outside-analysis-scope`) |

- **Row 28** relies on §5.2: the harness's own build fails, so Reach runs in default build mode.
- **Row 29** changes `Basics.Tools`. Ticket 12 said the `nothing-selected` rows prove all three
  reason codes; rows 14 and 15 cover only two, so this row covers the third. **Pin what Reach
  does**: if `Basics.Tools` raises a standing notice, it goes behind a baseline patch instead (§2.2).
  Implementation ticket 19 made Reach mark a change outside the analysis scope as such, so this row
  gives `all-changes-outside-analysis-scope`.

---

## 8. Migrating the Contoso acceptance tests

### 8.1 Where each test goes

From [ticket 06](issues/06-migrating-the-contoso-tests.md). There are 11 tests in
`tests/Reach.Tests/Acceptance/AcceptanceTests.cs`.

| Today's test | Becomes |
|---|---|
| `The_NUnit_filter_renders_with_contains_and_reports_the_extra_it_will_run` | row 7 |
| `An_empty_selection_is_skip_with_no_invocations_and_starts_no_process` | row 13, and the skip invariant (§4.4). Its consumer loop is dropped: it loops over an empty sequence, so the invariant *is* the claim |
| `An_unrecognised_framework_runs_in_full_with_an_unknown_total` | row 8, and the unknown-total invariant |
| `The_same_input_twice_produces_byte_identical_json` | **deleted**: `Expect` checks determinism on every test |
| `A_change_selects_exactly_the_tests_that_reach_it` | row 1 |
| `A_multi_targeted_project_contributes_an_entry_per_framework` | row 9. Its `Scope.Assemblies` check is dropped: the `@tfm` keys prove it |
| `The_generator_project_is_in_scope_and_not_expected_on_disk` | row 12. Its `Scope.Assemblies` check is dropped (see row 12's note) |
| `A_changed_const_consumed_across_an_assembly_boundary_selects_the_consumers_tests` | **retired into row 18**, which proves it exactly |
| `A_change_reaching_no_test_appears_with_a_count_of_zero` | row 15 |
| `Fact_added_to_an_existing_method_selects_it` | **misnamed**: its patch adds `[Trait]` to a method that already has `[Fact]`, which is row 2. Row 3 is the real case |
| `A_project_on_a_framework_needing_the_runner_setting_says_so` | row 11 |

Each is deleted in the change that lands its replacement, or later (§1.3).

### 8.2 The example report

`ExampleReportTests` stays C#. It runs over **Frameworks** with **row 7's patch file**, so the
example and a gate test cannot drift apart. Its file stays where it is. Expected consequences, to
regenerate and commit rather than treat as breakage:

- `docs/example-report.json` is regenerated. It grows (seven projects, not five: one production
  project and six test projects), and
  `test-runner-not-configured` drops out because Frameworks now has `global.json`. That code stays
  in the schema's index.
- The walk-through in `docs/report-schema.md` is updated to the new names and project count.

Because the example holds both a recognised and the unrecognised project, it is also what proves
the schema's `framework`/`dialect` claim (present and equal when recognised, both absent when not).

---

## 9. The release gate

### 9.1 The rules

From [ticket 03](issues/03-capabilities-and-the-release-gate.md) and
[ticket 12](issues/12-test-it-or-cut-it.md).

- **The gate is every claim the alpha's docs make.** A claim nobody will gate on comes out of the
  docs.
- **The docs in the gate** are `README.md`, `docs/adopting-reach.md`, `docs/report-schema.md`,
  `select --help`, and `docs/limitations.md` **only for its "Detectable" claims that name a notice
  code**. The register's gap descriptions and directions of failure are disclaimers, not promises.
- **A claim may be proven by any suite.** Acceptance tests run Reach in-process, so exit codes,
  argument parsing and delivery mechanics belong to the CLI and unit suites.
- **Claims about other people's tools are cited facts, outside the gate.** Each carries a source
  link. A claim without one is cut.
- **Known-gap tests are never in the gate.**
- **The table in §9.2 is hand-kept.** Each gate claim names the test that proves it. The parity
  tests (§10.3) cover every part that can be checked automatically. The last implementation ticket
  (§12, the gate audit) replaced every "ticket NN" in the table with the test's real name.

### 9.2 Claim to suite

Test names are relative to `tests/Reach.Tests/`. "Row N" is §7, and the first table below names
each row's test. "Every acceptance test" means the `Expect` invariants (§4.4), which
`Harness/ExpectationTests` proves can fail. Filled in by implementation ticket 16, the gate
audit; claims cut by implementation ticket 05 are gone from the table.

**Rows to tests.** `B` is `Acceptance/Basics/BasicsTests`, `F` is
`Acceptance/Frameworks/FrameworksTests`, `L` is `Acceptance/Ledger/LedgerTests`.

| Row | Test |
|---|---|
| 1 | `B.A_change_selects_exactly_the_tests_that_reach_it_through_compiled_edges` |
| 2 | `B.A_test_whose_own_body_changed_is_selected` |
| 3 | `B.An_existing_method_that_gains_Fact_is_selected` |
| 4 | `B.Interface_and_virtual_dispatch_select_every_candidate` |
| 5 | `B.A_deleted_method_widens_its_declaring_type` |
| 6 | `B.A_move_to_another_directory_in_the_same_namespace_widens_that_type` |
| 7 | `F.Each_dialect_renders_its_own_filter_and_NUnit_runs_more_than_it_selects` |
| 8 | `F.A_project_on_an_unrecognised_framework_runs_in_full_with_an_unknown_total` |
| 9 | `B.A_multi_targeted_test_project_gives_one_entry_per_framework` |
| 10 | `B.A_platform_suffixed_framework_makes_its_project_run_in_full` |
| 11 | `B.An_xunit_v3_4_project_without_the_runner_setting_gets_its_notice` |
| 12 | `B.A_change_to_a_generators_source_widens_its_consumers` |
| 13 | `B.A_formatting_only_change_is_no_changes` |
| 14 | `B.A_docs_only_change_selects_nothing_and_says_no_member_changed` |
| 15 | `B.A_changed_member_that_reaches_no_test_is_listed_with_a_count_of_zero` |
| 16 | `B.A_new_source_file_not_tracked_by_git_is_flagged` |
| 17 | `L.A_domain_change_reached_only_through_dunning_leaves_billing_out` |
| 18 | `L.A_changed_const_widens_only_the_projects_that_reference_its_assembly` (open: §7.2) |
| 19 | `L.A_removed_member_widens_only_the_projects_that_reference_its_assembly` |
| 20 | `L.A_project_file_change_widens_its_project_and_leaves_its_sibling_out` |
| 21 | `L.A_directory_build_props_change_widens_only_the_projects_beneath_it` |
| 22 | `L.A_namespace_move_widens_the_whole_assembly_and_no_further` |
| 23 | `L.A_root_file_matching_no_rule_selects_nothing_and_says_so` |
| 28 | `B.A_change_that_breaks_the_build_fails_with_exit_code_2` |
| 29 | `B.A_change_only_to_a_project_no_test_reaches_selects_nothing` |

Known gaps, outside the gate (§4.5): 24 `B.Known_gap_a_change_reached_only_through_Handler_of_Foo_selects_the_users_of_Handler_of_Bar`,
25 `B.Known_gap_a_ToString_reached_only_through_interpolation_selects_nothing`,
26 `L.Known_gap_a_change_to_one_channel_selects_the_callers_of_every_channel`,
27 `L.Known_gap_a_changed_const_in_the_core_assembly_selects_every_test`.

**`README.md`**

| Claim | Proven by |
|---|---|
| Walks the call graph backwards from changed code to tests | `Selection/SelectionTests.A_change_two_hops_from_a_test_selects_it`; row 1 |
| Over-selects where the implementation is not knowable | `Graph/WideningTests`; row 4 |
| Never narrows on unproven evidence | a principle: `Projects/AnalysisScopeTests.Scope_is_never_narrowed_to_the_dependents_of_a_changed_project`, and its instances, rows 17–23 |
| An empty selection exits `0` | every `nothing-selected` acceptance test (exit-code invariant): rows 14, 15, 29 |
| `dnx dotnet-reach@0.1.0-alpha.1` is the version this repo builds | `Reporting/WorkflowParityTests.Every_documented_dnx_line_pins_the_version_this_repository_builds` |
| The quick start's filter recipe | replaced by a link to `adopting-reach.md`'s recipe (implementation ticket 05), proven there below |
| The command line documents itself | `Cli/HelpGoldenTests.The_select_help_text_has_not_changed_by_accident`; `Reporting/HelpOptionParityTests.The_docs_describe_exactly_the_options_select_help_lists` |

**`docs/adopting-reach.md`**

| Claim | Proven by |
|---|---|
| A bare package id fails in `dnx` with *"not found in NuGet feeds"* | cited fact: `dotnet tool exec` docs and the SDK's message |
| The exit-code set is 0/1/2/3/4/5/70 | `Cli/ExitCodeTests.The_set_is_exactly_the_one_the_specification_names` |
| 1: usage error, writes no report | `Cli/CommandLineTests.An_unknown_option_is_a_usage_error`, `A_usage_error_writes_nothing_into_the_directory_Reach_owns` |
| 2: build failed | row 28; `Build/BuildAdapterTests.A_failed_build_is_a_result_carrying_what_the_build_said` |
| 3: missing or ambiguous assembly | `Assemblies/AssemblyDiscoveryTests.Debug_and_Release_both_present_is_exit_3_naming_the_option_that_settles_it`, `A_missing_expected_assembly_is_exit_3`; `Cli/CommandLineTests.Nothing_built_under_no_build_is_exit_3_and_still_writes_a_report` |
| 4: baseline unresolvable or shallow, prints the line to add | `Baselines/BaselineResolverIntegrationTests.A_shallow_clone_is_exit_4`; `Baselines/BaselineResolverTests.Exit_4_under_GitHub_Actions_prints_the_line_to_add`, `Exit_4_under_Azure_DevOps_prints_the_line_to_add`, `Exit_4_off_a_known_provider_says_how_to_fix_it_anyway`; `Cli/CommandLineTests.An_unresolvable_baseline_is_exit_4`, `Exit_4_under_a_simulated_GitHub_Actions_environment_prints_the_line_to_add` |
| 5: source and binary do not correspond | `Assemblies/CorrespondenceInBothModesTests.An_edited_file_under_no_build_is_exit_5_naming_the_document` |
| 70: internal error | `Cli/ExitPathTests.A_pipeline_that_throws_is_exit_70` |
| An empty selection emits no command | `Rendering/InvocationTests.An_empty_selection_emits_no_command_at_all`; `Rendering/ConsumerLoopTests.A_loop_over_an_empty_selection_starts_no_process_at_all`; every acceptance test (skip invariant) |
| Every chunk carries at least one test | `Rendering/RenderingTests.The_chunker_partitions_the_set_with_nothing_dropped_or_duplicated`, `An_empty_selection_produces_no_chunks` |
| A multi-targeted project pins its framework | `Rendering/InvocationTests.Every_invocation_for_a_multi_instance_project_carries_a_matching_selector`; row 9 |
| `--ignore-exit-code 8` and the .NET 11 change; `--zero-tests-policy strict` | cited facts: MTP docs, `dotnet test` (MTP) docs, microsoft/testfx#7457 |
| A VSTest project under a testing-platform `global.json` cannot run | cited fact: `dotnet test` (MTP) docs. Reach's part (no check, no notice) is a disclaimer, registered in `limitations.md` (owner decision, implementation ticket 16) |
| A shallow checkout is exit 4 | `Baselines/BaselineResolverTests.A_shallow_clone_is_exit_4_before_anything_else_is_tried`; `Baselines/BaselineResolverIntegrationTests.A_shallow_clone_is_exit_4` |
| The recipe is `dogfood.yml`, and one character of drift fails | `Reporting/WorkflowParityTests.The_documented_recipe_is_the_workflow_this_repository_runs`, `One_changed_character_in_the_documented_recipe_fails_the_comparison`, `The_documented_recipe_fetches_the_whole_history`, `The_documented_recipe_pins_the_version` |
| The recipe's loop runs nothing when empty, propagates failure, is not swallowed by stdin | the dogfood workflow runs it. **Proven only once `0.1.0-alpha.1` is published**: until then the workflow fails at `dnx`, which is expected and does not block publishing |
| `dnx` is put on `PATH` by the .NET 10 SDK installer | cited fact: `dotnet tool exec` docs |
| `jq` is preinstalled; `@sh` quotes argv | cited facts: the runner image readme, the jq manual |
| Detection variables for five providers | `Baselines/BaselineResolverTests.Rung_1_is_GITHUB_BASE_REF`, `Rung_2_is_the_Azure_DevOps_target_branch`, `Rung_3_is_the_GitLab_merge_request_target`, `Rung_4_is_the_Bitbucket_destination_branch`, `Rung_5_is_the_Jenkins_change_target`, `The_option_wins_over_every_variable` |
| Fetch-history settings for the five non-GitHub providers | cited facts: each provider's docs |
| TeamCity is not detected | `Baselines/BaselineResolverTests.TeamCity_is_not_detected` |
| Never reads a source-branch variable | `Baselines/BaselineResolverTests.GITHUB_HEAD_REF_is_never_read` and its five siblings |
| `-c` and `-v` take `dotnet build`'s values; `--no-color` turns colour off | `Cli/CommandLineTests.Verbosity_mirrors_the_SDKs_spellings`, `Every_option_reaches_the_request`, `No_color_and_NO_COLOR_and_a_redirected_stdout_each_suppress_colour` |
| `--no-build` checks checksums in both modes | `Assemblies/CorrespondenceInBothModesTests` (all three) |
| Git does not preserve modification times; MSBuild's incremental build compares timestamps | cited facts: the Git FAQ, MSBuild's incremental-builds docs |
| `--runsettings` merges; an existing filter runs the project in full | `Rendering/InvocationTests.A_runsettings_file_without_a_filter_is_merged`, `A_runsettings_file_that_already_filters_downgrades_to_run_all` |
| `-o` and `--artifacts-path` narrow the scan | `Assemblies/AssemblyDiscoveryTests.The_output_hint_breaks_it_too`, `The_artifacts_path_hint_breaks_it_too` |
| `--report` / `--report-dir` are where Reach writes | `Cli/CommandLineTests.Report_dir_relocates_the_whole_directory_including_side_car_files`, `Report_moves_the_json_alone_and_leaves_the_directory_where_it_was` |
| A layout switch after `--` is exit 1 | `Cli/CommandLineTests.A_layout_switch_after_the_boundary_is_exit_1_naming_the_Reach_option`, `A_layout_switch_written_with_an_equals_sign_is_refused_too` |
| A directory move widens the type; a namespace move widens the assembly | `Changes/ChangedSetTests.A_type_moved_between_directories_yields_whole_type_widening`, `A_type_moved_between_namespaces_yields_whole_assembly_widening`; rows 6, 22 |
| `.reach/` and its `.gitignore` on first use; an existing one left alone | `Cli/CommandLineTests.A_run_that_gets_past_usage_creates_the_directory_with_a_gitignore`, `An_existing_gitignore_is_left_alone` |
| Reach never deletes its files | `Rendering/InvocationTests.Re_running_overwrites_a_side_car_rather_than_accumulating`; `Acceptance/ReachDirectorySurvivalTests.The_report_and_side_car_files_remain_after_the_process_exits` |

**`docs/report-schema.md`**

| Claim | Proven by |
|---|---|
| `example-report.json` is the determinism fixture's output | `Acceptance/ExampleReportTests.The_committed_example_is_what_row_7s_patch_produces` |
| `schemaVersion` is `1` | `Reporting/ReportTests.The_schema_version_is_one_and_cannot_be_anything_else` |
| Deterministic; arrays sorted; only timings differ | `Reporting/ReportTests.The_same_input_twice_produces_byte_identical_json`, `Every_array_is_sorted_by_its_documented_key`, `Only_the_timings_differ_between_two_runs`; every acceptance test |
| A report is written for every outcome, including failure | exit 0: `Cli/CommandLineTests.A_complete_run_exits_0_and_writes_a_report`; exit 3: `Nothing_built_under_no_build_is_exit_3_and_still_writes_a_report`; exits 2, 4, 5, 70: `Cli/ExitPathTests.Exit_2_writes_a_failed_report_with_no_summary`, `Exit_4_…`, `Exit_5_…`, `Exit_70_…` |
| …except exit 1 | `Cli/CommandLineTests.A_usage_error_writes_nothing_into_the_directory_Reach_owns` |
| `outcome` is a closed set of four | `Reporting/ReportTests.The_four_outcomes_are_a_closed_set` |
| `reasons` present and non-empty exactly when `nothing-selected` | `Reporting/ReportTests.Nothing_selected_always_carries_a_non_empty_reasons_array`, `A_selected_run_carries_no_reasons`; rows 14, 15, 29 |
| `scope` carries identities, never closure edges | `Reporting/ReportTests.Scope_carries_identities_and_never_closure_edges` |
| `summary` absent when the run failed before rendering | `Cli/ExitPathTests.Exit_2_writes_a_failed_report_with_no_summary` and its exit 4, 5 and 70 siblings |
| `no-changes` and `nothing-selected` differ; same exit | `Reporting/ReportTests.No_changes_and_nothing_selected_are_different_outcomes`; `Reporting/HumanSummaryTests.No_changes_and_nothing_selected_get_visibly_different_verdicts`; exit-code invariant |
| A formatting-only C# edit is `no-changes`; a README-only change is `nothing-selected` | rows 13, 14 |
| On `no-changes` every project is `skip` with zero invocations | `Reporting/ReportTests.No_changes_still_emits_every_test_project_as_skip`; `Rendering/ConsumerLoopTests.A_loop_over_a_no_changes_report_starts_no_process_either`; row 13 |
| `envelope.baseline` is a SHA with `detectedFrom` and `isHead` | `Baselines/BaselineResolverTests.Every_resolution_is_disclosed_with_the_sha_it_landed_on`, `A_baseline_resolving_to_HEAD_emits_its_own_notice` |
| …absent on exit 4; `envelope.head` absent when git cannot be read | `Cli/ExitPathTests.Exit_4_writes_no_baseline_into_the_envelope`, `A_head_git_cannot_read_is_absent_from_the_envelope` |
| `changeSources`, `buildMode`, `forwardedBuildArguments`, `correspondence` in the report | `Acceptance/ExampleReportTests.The_committed_example_is_what_row_7s_patch_produces` (the example carries all four) |
| One entry per (project, framework) | `Reporting/ReportTests.A_single_targeted_project_still_produces_a_project_and_framework_keyed_entry`; row 9 |
| `framework` and `dialect` both absent when unrecognised, equal otherwise | `Acceptance/ExampleReportTests.The_committed_example_is_what_row_7s_patch_produces` (§8.2); `Reporting/WalkThroughParityTests.Every_value_the_walk_through_quotes_is_the_example_s` |
| `runnerHost`: its value, absent on `skip`, present on run-all | `Reporting/ReportEntryTests.A_filtered_entry_names_dotnet_test_as_its_runner_host`, `A_skip_entry_carries_no_runner_host`, `A_project_run_in_full_still_names_its_runner_host` |
| `delivery` values; absent on an entry with no command | `Rendering/InvocationTests.A_long_selection_on_the_testing_platform_goes_through_a_response_file`, `A_long_selection_on_a_host_with_no_channel_is_chunked`; `Reporting/ReportEntryTests.A_short_selection_is_delivered_inline`, `A_selection_merged_into_a_runsettings_file_is_delivered_by_run_settings`, `A_skip_entry_carries_no_delivery` |
| `invocations`: many when chunked, one for run-all, zero for skip | `Rendering/InvocationTests.A_long_selection_on_a_host_with_no_channel_is_chunked`, `Run_all_emits_one_invocation_with_no_filter`; `Reporting/ReportTests.Invocations_is_always_an_array_and_empty_for_skip` |
| An underivable moniker runs once, the rest empty with no `delivery` | `Rendering/InvocationTests.A_multi_instance_project_with_a_platform_suffix_runs_project_wide_once`; row 10; `Reporting/ReportEntryTests.The_empty_entries_of_an_underivable_moniker_carry_no_delivery` |
| `counts.total` is `"unknown"`, never `0` | `Reporting/ReportTests.An_unknown_total_round_trips_and_is_distinguishable_from_zero`; row 8; every acceptance test |
| …and `willRun` is `0` on that entry | `Reporting/ReportEntryTests.An_entry_whose_total_is_unknown_will_run_zero` |
| `projectsRunInFull` counts those entries | `Reporting/MeasurementTests.An_unenumerable_project_contributes_to_neither_side`, `The_summary_line_carries_both_numbers` |
| `tests[].rules` and their three values | `Selection/SelectionTests.A_changed_test_method_selects_itself`, `A_newly_added_test_selects_with_new_since_baseline_and_an_empty_roots_list`; rows 1–3 |
| `changes` empty and `pathClass` absent exactly when not `reverse-reachable` | `Selection/SelectionTests.A_newly_added_test_selects_with_new_since_baseline_and_an_empty_roots_list`, `A_test_selected_by_a_change_elsewhere_carries_a_root_and_a_class` |
| `pathClass` is the weakest edge on the strongest path | `Selection/ReverseWalkTests.A_test_with_both_a_compiled_and_a_widened_path_reads_compiled`, `A_test_reachable_only_through_a_widened_edge_reads_widened` |
| `paths` only under `--paths`, every hop a method | `Selection/SelectionTests.Paths_are_off_by_default`, `Paths_produce_a_hop_sequence_from_the_test_back_to_the_change` |
| `tests[].id` is stable for the same input | `Reporting/ReportTests.The_same_input_twice_produces_byte_identical_json`; every acceptance test (determinism) |
| `changes` keyed on changes, not expanded roots | `Selection/WholeProjectAndWideningTests.Whole_assembly_widening_contributes_one_entry_whatever_it_expands_to` |
| `tier` values | `Selection/SelectionTests.A_member_change_is_tiered_as_a_member`; `Changes/ChangedSetTests`; rows 5, 6, 22 |
| `testsReached` counts, including `0` | `Selection/SelectionTests.A_change_reaching_no_test_at_all_appears_with_a_count_of_zero`, `A_change_that_reaches_a_test_carries_the_count`; row 15 |
| A change outside the analysis scope is listed with `testsReached: 0` and a reason saying so | `Reporting/ReportTests.A_change_only_outside_the_analysis_scope_says_so_rather_than_that_it_reached_no_test`, `An_out_of_scope_change_beside_an_in_scope_one_that_reached_nothing_keeps_that_reason`; row 29 |
| `summary.willRun` is the numerator; `widenedPairs` | `Reporting/MeasurementTests.The_numerator_is_what_will_run_not_what_was_selected`, `The_widening_delta_counts_pairs_whose_path_class_is_widened` |
| `ratio` to four places, or `null` | `Reporting/MeasurementTests.Nothing_enumerable_reports_an_unknown_ratio`, `The_ratio_is_rounded_to_four_places_in_the_report` |
| The walk-through's quoted values | `Reporting/WalkThroughParityTests.Every_value_the_walk_through_quotes_is_the_example_s`, `One_changed_value_in_the_walk_through_fails_the_comparison` |
| Every `blind-spot` code has a register entry | `Reporting/RegisterParityTests.Every_blind_spot_code_has_a_register_entry`, `The_parity_test_fails_when_a_blind_spot_code_is_unregistered` |
| The notice index lists every code Reach can emit, under the right kind | `Reporting/NoticeIndexParityTests.The_index_lists_every_code_Reach_can_emit_under_its_kind`, `One_renamed_code_in_the_index_fails_the_comparison`, `A_code_under_the_wrong_kind_fails_the_comparison` |
| Notices cannot be suppressed | `Cli/HelpGoldenTests.The_select_help_text_has_not_changed_by_accident`; `Reporting/HelpOptionParityTests.An_option_the_docs_do_not_describe_fails_the_comparison` |

**`select --help`**

| Claim | Proven by |
|---|---|
| The help text, and the options it lists | `Cli/HelpGoldenTests.The_select_help_text_has_not_changed_by_accident`; `Reporting/HelpOptionParityTests` (all three) |
| The target is discovered in the working directory when omitted | `Projects/TargetDiscoveryTests` |
| `--base` is auto-detected when omitted | `Baselines/BaselineResolverTests.Rung_1_is_GITHUB_BASE_REF` … `Rung_7_is_exit_4` |
| `--report -` writes the JSON to standard output and the summary to standard error | `Cli/CommandLineTests.Report_dash_puts_the_json_on_stdout_and_moves_the_summary_to_stderr` |
| `--report-dir` defaults to `.reach` under the working directory | `Cli/CommandLineTests.The_report_defaults_into_the_directory_Reach_owns` |
| `--no-build` analyses the output on disk without building | `Assemblies/CorrespondenceInBothModesTests.No_build_never_invokes_the_build` |
| `--no-color`, and `NO_COLOR` is honoured | `Cli/CommandLineTests.No_color_and_NO_COLOR_and_a_redirected_stdout_each_suppress_colour`, `An_empty_NO_COLOR_does_not_suppress_colour` |
| `--paths`, `--runsettings`, `-o`, `--artifacts-path` | as `report-schema.md` and `adopting-reach.md` above |

**Per notice code.** Each code is proven by a test that makes Reach emit it and asserts it. These
are also the codes `limitations.md`'s "Detectable" claims name.

| Code | Proven by |
|---|---|
| `ignored-untracked-assembly` | `Assemblies/CorrespondenceTests.A_first_party_document_that_is_untracked_and_ignored_is_disclosed` |
| `unmapped-file-no-project` | `Changes/TierLadderTests.That_case_emits_its_own_notice_and_names_the_file`; rows 14, 23 |
| `parse-failed` | `Changes/ParseHealthTests.A_file_that_does_not_parse_widens_its_project_and_says_so` |
| `unresolved-first-party-member` | `Graph/CallGraphTests.An_unresolvable_first_party_member_is_a_notice_and_does_not_stop_the_run` |
| `signature-ambiguous` | `Graph/CallGraphTests.An_ambiguous_signature_edges_to_every_candidate_and_says_so` |
| `whole-project-fallback` | row 8 |
| `nunit-filter-overmatch`, `dialect-over-selects` | `Rendering/InvocationTests.A_contains_dialect_reports_the_extra_it_will_run`; row 7 |
| `runsettings-filter-conflict` | `Rendering/InvocationTests.A_runsettings_file_that_already_filters_downgrades_to_run_all` |
| `framework-selector-underivable` | `Rendering/InvocationTests.A_multi_instance_project_with_a_platform_suffix_runs_project_wide_once`; row 10 |
| `recompilation-widening` | `Changes/RecompilationWideningTests.Each_triggering_change_raises_its_own_widening_notice_naming_the_member_and_its_consumers`; rows 18, 19 |
| `project-outside-every-solution` | `Projects/AnalysisScopeTests.A_project_outside_every_solution_emits_the_scope_notice_and_continues` |
| `untracked-source-files` | `Changes/ChangedSetTests.An_untracked_source_file_is_disclosed`; row 16 |
| `no-changed-member-reached-a-test` | row 15 (`Reasons`) |
| `all-changes-outside-analysis-scope` | row 29 (`Reasons`); `Reporting/ReportTests.A_change_only_outside_the_analysis_scope_says_so_rather_than_that_it_reached_no_test` |
| `changes-were-formatting-only` | row 14 (`Reasons`) |
| the three above go in `reasons`, not `notices` | rows 14, 15, 29 (exact `Notices` and `Reasons`) |
| `baseline-resolved`, `baseline-is-head`, `baseline-unresolvable` | `Baselines/BaselineResolverTests.Every_resolution_is_disclosed_with_the_sha_it_landed_on`, `A_baseline_resolving_to_HEAD_emits_its_own_notice`, `A_failed_resolution_is_disclosed_too` |
| `langversion-above-ceiling` | `Changes/ParseHealthTests.A_project_declaring_a_language_version_above_the_ceiling_raises_an_environment_notice`; `Projects/AnalysisScopeTests.A_language_version_above_the_ceiling_is_reported_including_from_Directory_Build_props` |
| `selection-delivered-out-of-band` | `Rendering/InvocationTests.A_long_selection_on_the_testing_platform_goes_through_a_response_file`, `A_long_selection_on_a_host_with_no_channel_is_chunked` |
| `test-runner-not-configured` | row 11 |
| `unmapped-deterministic-source-root`, its `data.roots`, and packages folders configured other ways | `Assemblies/CorrespondenceTests.A_further_deterministic_root_is_not_guessed_at_and_says_so`, `The_unmapped_root_notice_carries_every_root_it_left_unmapped_as_data`, `Package_content_in_a_global_packages_folder_set_in_nuget_config_raises_the_notice`, `Package_content_in_a_RestorePackagesPath_folder_raises_the_notice`, `Package_content_in_a_fallback_packages_folder_raises_the_notice` |

---

## 10. Changes to Reach and its docs

All from [ticket 12](issues/12-test-it-or-cut-it.md), except where marked.

### 10.1 Implemented

- **`recompilation-widening` is emitted.** The widening exists (`RecompilationWidening.From`); the
  work is to raise the notice whenever it fires. Rows 18 and 19 expect it.
- **`langversion-above-ceiling` is emitted**, from the existing `ExceedsCeiling` check. It is
  reported, not widened, as the schema says.

### 10.2 Cut

- **`calli-unresolved`**: removed from the schema's notice index and from `NoticeCodes` and
  `NoticeCatalogue`. The function-pointer register entry says "Detectable: no". Detecting it is new
  IL dataflow work, after the alpha.
- **`packageVersion`**: the schema row and the unused `ReportEntry.PackageVersion` property are
  deleted. `dialect` already records what the version decided, and Reach knows only the referenced
  assembly's version, which can differ from the package's.
- **`--list-unselected`** (owner decision while writing this spec): it is parsed and never read, so
  the option, its help line and `SelectRequest.ListUnselected` are deleted. M1 spec §13.5 is
  amended to "counted, never enumerated" (§11).
- **README's `dotnet test --filter "$(…)"`**: replaced with the real recipe from
  `adopting-reach.md`, or a link to it. Reach gets no new output mode.
- **Group D claims without a source link** are cut. Those with one stay as cited facts.
- **Two wordings no test can prove**, found while writing this spec (both missed by ticket 11's
  unproven list): `tests[].id` being "comparable across runs" is narrowed to "stable for the same
  input", which determinism proves; the claim that reserved `data` keys mean the same thing
  everywhere is cut or narrowed to what a test checks.

### 10.3 Help, and the parity tests

- **`select --help` is in the gate.** The `--paths` description is fixed by hand to match the
  schema ("adds hop-by-hop paths"). The descriptions stay hand-kept.
- **Three new parity tests**, beside `RegisterParityTests` and `WorkflowParityTests`:
  1. **The notice index**: the codes and kinds in `docs/report-schema.md`'s index equal
     `NoticeCatalogue`'s. It would have caught §10.1 and §10.2's notice codes.
  2. **The walk-through**: the values the schema's walk-through quotes equal
     `docs/example-report.json`.
  3. **The help options**: the option names in `select --help` equal the options the docs
     describe.

### 10.4 Tests in other suites

| Claim | Suite |
|---|---|
| Exit 70; a report on exits 2, 4, 5, 70; `summary` absent on early failure | CLI tests, one per exit code. Exit 70 needs the pipeline to throw in a test |
| `envelope.baseline` absent on exit 4; `envelope.head` absent when git is unreadable | CLI tests, same class |
| `runnerHost`, `delivery`, `willRun` 0 on an unknown total, `ratio` to four places | report-writer unit tests |
| `selection-delivered-out-of-band` | an assertion added to the existing long-selection CLI test |
| TeamCity not detected | a unit test on CI detection |
| `--artifacts-path` narrows discovery | a unit test on discovery |
| `.reach/` files remain after the process exits | one out-of-process smoke test |
| `changeSources`, `buildMode`, `forwardedBuildArguments`, `correspondence` | the example-report parity, or report-writer tests for any the example lacks |
| `unmapped-deterministic-source-root`'s `data.roots`; the notice for packages folders configured other ways | assertions in `CorrespondenceTests` |

---

## 11. Amendments to other records

- **[M1 spec](../walking-skeleton/spec.md) §13.5 and its §4 option table**: `--list-unselected` is
  removed. Unselected tests are counted, never enumerated.
- **[Publishing](../m1-implementation/issues/21-publishing.md)**: its `On hold:` line is replaced by
  a dependency on the gate audit (§12, ticket 16). Planning is done; the gate itself is what the
  release now waits for. (Done when this spec was written.)
- **[docs/limitations.md](../../docs/limitations.md)**: the function-pointer entry's "Detectable"
  line changes (§10.2).

---

## 12. Implementation tickets

The tickets live in a separate effort, [`.scratch/acceptance-implementation/`](../acceptance-implementation/README.md),
so a future wayfinder session never mistakes a build ticket for a decision. Each is sized to one
agent session, numbered in landing order.

**Stage A: the harness.**

| # | Ticket |
|---|---|
| 01 | Test running: full parallelism and the two CI steps |
| 02 | The harness, and Basics' first row |
| 03 | The patch tool and the stripped-form guard |

**Stage B: what the gate needs from Reach and its docs.**

| # | Ticket |
|---|---|
| 04 | Emit `recompilation-widening` and `langversion-above-ceiling` |
| 05 | Cut and cite: what the docs stop claiming |

**Stage C: the samples and their rows.**

| # | Ticket |
|---|---|
| 06 | Basics: selection and tier rows (2–6) |
| 07 | Basics: outcome and reason rows (12–16, 28, 29) |
| 08 | Basics: starting-state variants (9–11) |
| 09 | Basics: known gaps and the known-gap guard (24, 25) |
| 10 | Ledger: the sample and its containment rows (17, 20–23) |
| 11 | Ledger: recompilation rows and known gaps (18, 19, 26, 27) |
| 12 | Frameworks: rows 7 and 8, the example report, and the end of `tests/fixtures/` |

**Stage D: the rest of the gate.**

| # | Ticket |
|---|---|
| 13 | Exit-path CLI tests |
| 14 | Report-writer and unit-level tests |
| 15 | Parity tests: the notice index, the walk-through and the help |
| 16 | The gate audit |

---

## 13. Where each decision comes from

| Area | Record |
|---|---|
| Three jobs, exact oracle, replacing Contoso | [the map's Decisions so far](map.md#decisions-so-far) |
| Solution membership and the coding standard | [coding-standards](../coding-standards/spec.md) |
| The patch format | [ticket 01](issues/01-patch-format-and-readability.md) |
| Build cost, parallel facts | [ticket 02](issues/02-building-a-patched-sample-cheaply.md) — corrected by ticket 09 on xUnit parallelism |
| Rows 1–27, the gate rule, the wider expected result | [ticket 03](issues/03-capabilities-and-the-release-gate.md) — **read its Comments**: row 3 changed |
| Code not data, `Apply`/`Expect`, keys, known gaps | [ticket 04](issues/04-how-an-acceptance-test-is-declared.md) — **read its Comments**: three amendments |
| Ledger | [ticket 05](issues/05-the-first-multi-project-sample-solution.md) |
| The Contoso migration, the two invariants, the example report | [ticket 06](issues/06-migrating-the-contoso-tests.md) |
| Where the tests run, CI | [ticket 07](issues/07-where-acceptance-tests-run.md) |
| The patch tool, independence, drift | [ticket 08](issues/08-writing-a-patch-and-keeping-it-applying.md) — **read its Comments**: independence is per pair |
| Build shape, baseline, parallelism, determinism | [ticket 09](issues/09-how-the-harness-builds-each-acceptance-test.md) — **read its Comments**: the baseline patch |
| Basics, Frameworks, baseline patches | [ticket 10](issues/10-the-small-sample-solutions.md) |
| The claim audit | [ticket 11](issues/11-which-suite-proves-each-claim.md) |
| Test it or cut it | [ticket 12](issues/12-test-it-or-cut-it.md) |
| Rows 28–29, §5.2's build mode, `--list-unselected`, two narrowed wordings | this spec, [ticket 13](issues/13-write-the-spec-and-cut-the-tickets.md) |
