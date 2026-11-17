# Migrating the Contoso acceptance tests

Type: grilling
Status: resolved
Blocked by: (none — 04, 10 resolved)

## Question

Contoso becomes the first small sample solution, and its 13 acceptance tests
(`tests/Reach.Tests/Acceptance/AcceptanceTests.cs`) move onto patches. Not all of them are
selection checks: report determinism, the consumer loop over `invocations`, and the committed
example report (`ExampleReportTests`) assert other things.

For each of the 13 plus `ExampleReportTests`: does it become an acceptance test, stay as C#
over the new harness, or move elsewhere? And does the Contoso fixture itself need to change
to fit the declaration format?

## Answer

Settled with the owner in one round of grilling (2026-11-17). Every recommendation was taken.

### Where each test goes

| # | Today's test | Becomes | Sample |
|---|---|---|---|
| 1 | NUnit `~` renders contains, will-run > selected | row 7 | Frameworks |
| 2 | Empty selection: `skip`, zero invocations, no process | row 13, plus an `Expect` invariant | Basics |
| 3 | Unrecognised framework runs in full, total `unknown` | row 8, plus an `Expect` invariant | Frameworks |
| 4 | Determinism | **deleted**: `Expect` checks it on every test | — |
| 5 | Exact selection (`Totals_include_tax` only) | row 1 | Basics |
| 6 | Multi-targeted project gives an entry per framework | row 9 (baseline patch) | Basics |
| 7 | Generator in scope, not expected on disk | row 12 | Basics |
| 8 | `const` across an assembly boundary | **retired into row 18** | Ledger |
| 9 | Change reaching no test has count 0 | row 15 | Basics |
| 10 | "`[Fact]` added to an existing method" | **row 3, rewritten** (see below) | Basics |
| 11 | Runner-setting notice | row 11 (baseline patch) | Basics |
| — | `ExampleReportTests` | stays C#, over Frameworks with row 7's patch | Frameworks |

### Assertions `Expected` cannot express

- **Two new invariants inside `Expect`**, checked on every acceptance test the same way
  determinism is:
  - a project with nothing selected that does not run in full has mode `skip` and **zero
    invocations**;
  - a project run in full serialises `"total": "unknown"`, never `0`.

  Both are true of every report, not of one patch, and both are among the walking skeleton's four
  silent-failure assertions. Checking them on every test makes them stronger than one test each.
- **Dropped: the consumer loop** in test 2 (`ScriptedProcessRunner` over the invocations). It
  iterates over an empty sequence, so the zero-invocation invariant *is* the claim.
- **Dropped: the `report.Scope.Assemblies` checks** in tests 6 and 7. Row 9's
  `Name@net8.0` / `Name@net10.0` keys prove one entry per framework. If the generator were wrongly
  expected on disk, every Basics test would end `failed`, and the required `Outcome` catches that.
  Row 12's `Because` says so.

### The two that did not fit

- **The `const` test retires into row 18.** It asserted only that the consumer's tests were
  non-empty. Ledger's row 18 proves the same thing exactly, including what is *not* selected. A
  small-sample copy would be weaker, and ticket 03 puts "widens only its referencers" on the
  multi-project sample.
- **Test 10 was misnamed.** Its patch adds `[Trait]` to a method that already has `[Fact]`, which is
  row 2. The case walking-skeleton ticket 07 escalated (an existing *non-test* method, body
  unchanged, gains `[Fact]`) had no test at all. **Row 3's patch is now that attribute-only case.**
  A brand-new test method is not tested separately: it is a strict superset (new declaration and
  new body), so the harder case covers it. Row 2 gets its own body-change patch.

### The example report

`ExampleReportTests` runs over **Frameworks** with **row 7's patch file** (the same `Tax` change),
so the example and a gate test cannot drift apart. Its file stays where it is. Expected
consequences, which the implementation regenerates and updates rather than treating as breakage:

- `docs/example-report.json` is regenerated. It grows (eight projects, not five), and
  `test-runner-not-configured` drops out, because Frameworks now has `global.json`. That code stays
  documented in the code index.
- The walk in `docs/report-schema.md` is updated to the new names and project count.

Basics was rejected because it loses the `filtered` / `run-all` / over-match side-by-side the walk
exists to explain. A dedicated sample was rejected as one more thing to keep current.

### Does the fixture change?

Beyond ticket 10's changes, **everything is renamed to `Frameworks.*`**: the solution, projects
and namespaces. Test projects are named by dialect: `Frameworks.Tests.XunitV2`, `.XunitV3`,
`.XunitV3_4`, `.NUnit`, `.MSTest`, `.Unrecognised`. Then a `Selected` key says which dialect it
proves. The in-memory tests that use "Contoso" as a made-up name are unrelated and stay.

### No gap during the move

**One constraint for the spec:** each old test is deleted in the same change that lands its
replacement, or later, and never earlier. So none of the four silent-failure assertions is ever
without a test. The order of the implementation tickets is the spec's job.

### Not changed

No `CONTEXT.md` change and no ADR: test infrastructure, reversible.

### Amends

- [How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md): `Expect` checks
  two more invariants on every test.
- [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md):
  row 3's patch is the attribute-only case.

## Comments

- **From [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md):** there are **11** Contoso acceptance tests, not 13. The four that assert more
  than selection (NUnit `~`, determinism, zero-count change, runner-setting notice) now fit the
  layer: the expected result gains will-run, notices and changes, and **every** acceptance test
  checks determinism. Whether Contoso becomes the "frameworks" sample is decided in
  [The small sample solutions](10-the-small-sample-solutions.md).
- **From [How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md):**
  Contoso moves from `tests/fixtures/Contoso/` to `tests/samples/Contoso/`, and its tests become
  one class in `tests/Reach.Tests/Acceptance/Contoso/` with `.patch` files beside it; today's
  `Touch` calls become patches. `tests/fixtures/` goes once it is empty.
- **From [How the harness builds each acceptance test](09-how-the-harness-builds-each-acceptance-test.md):**
  `FixtureSolution` is **reworked**, not deleted, into the per-test sample copy. This migration
  removes its old API (`WriteFile`/`Touch`/`Rebuild`) once no caller is left. Determinism is
  checked inside `Expect` with `timings` scrubbed, so today's standalone determinism test has
  nothing left to assert.
- **From [The small sample solutions](10-the-small-sample-solutions.md):** Contoso becomes **Frameworks** (`tests/samples/Frameworks/`). It gains
  `xunit-v2`, `xunit-v3` 3.x and MSTest test projects and a `global.json` with the runner
  setting; `Contoso.Core` drops its multi-targeting (rows 9 and 10 become baseline patches on **Basics**) and the generator project
  moves to **Basics**. Today's runner-setting test becomes row 11 on Basics, via a baseline patch
  that deletes `global.json`. Any Contoso test leaning on multi-targeting or the generator moves
  with them.
