# How an acceptance test is declared

Type: grilling
Status: resolved
Blocked by: (none — 01 resolved)

## Question

Given the patch format chosen in [How a patch and its expected result read](01-patch-format-and-readability.md):
is an acceptance test **data** — one folder per test (patch, expected result, a short
README saying what it shows), all discovered and run by a single xUnit theory — or **code**,
one C# test method per acceptance test with its patch alongside?

Settle too: where sample solutions and acceptance tests live in the tree, how an acceptance
test names its sample solution, and the exact shape of the expected result's file.

## Comments

- **From [How a patch and its expected result read](01-patch-format-and-readability.md):** the
  patch is a plain unified diff (`change.patch`). Two questions the prototype raised belong
  here:
  - **Must an acceptance test give a `because` line?** That is a short free-text reason for
    its expected selection. In the prototype's rename-and-delete test, nothing in the patch
    explains why `Net_is_the_sum_of_the_lines` is selected. The reason is whole-assembly
    widening, from walking-skeleton spec §9.3.
  - **Use `report.json`'s vocabulary in the expected result.** The map's `ok` / `skip` /
    `failed` are not the schema's words. The prototype used `outcome`
    (`selected` · `nothing-selected` · `no-changes` · `failed`), full `display` names grouped
    by test project, and `runInFull`. Its `expected.json` is on branch
    `prototype/patch-format`.
- **From [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md):**
  - The expected result gains **optional** sections: **will run**, **notices** (codes),
    **changes** (member + test count) and **tier** per change. A section that is given is
    **exact**. No exit code — acceptance tests stay in-process.
  - **Known-gap tests** must be declarable and visibly named as gaps (e.g.
    `Known_gap_interpolated_ToString_is_not_selected`), and kept out of the release gate — the
    declaration needs some way to say which batch a test is in.

## Answer

Settled with the owner over four rounds of grilling.

### Code, not data

**An acceptance test is one C# test method.** Chosen for reuse: a test may share patches,
pieces of expected results and harness steps, as it needs. A folder of data run by one theory
was the alternative.

### Where things live

- **Sample solutions move to `tests/samples/<Name>/`**, one folder each with its own `.slnx`
  and README. `tests/fixtures/` goes when Contoso has moved (how is
  [Migrating the Contoso tests](06-migrating-the-contoso-tests.md)'s question). A sample is
  still never built in place.
- **No acceptance test lives inside a sample solution's folder.** That folder becomes the
  baseline repository Reach reads, so it holds the sample solution and nothing else.
- **One folder per sample solution at `tests/Reach.Tests/Acceptance/<Name>/`**, holding one
  test class and that sample's `.patch` files, flat — no `patches/` subfolder. The class names
  its sample solution once, never per test. Today's `Acceptance/AcceptanceTests.cs` is replaced;
  `ExampleReportTests.cs` stays. The namespace filter `Reach.Tests.Acceptance` still selects the
  group.

### How a test reads

```csharp
[Fact]
public async Task A_late_fee_change_selects_dunning_but_not_billing() =>
    await Apply("late-fee-counts-part-months.patch").Expect(new()
    {
        Because = "Only Invoice.LateFee changed; Dunning reaches it, Billing and the API don't.",
        Outcome = Outcome.Selected,
        Selected = new()
        {
            ["Ledger.Domain.Tests"] = ["Ledger.Domain.Tests.LateFeeTests.Two_months_late_charges_three_percent"],
            ["Ledger.Services.Tests"] = ["Ledger.Services.Tests.DunningTests.A_reminder_quotes_the_late_fee"],
        },
        RunInFull = [],
        Tiers = new() { ["Ledger.Domain.Invoice.LateFee(DateOnly)"] = Tier.Member },
    });
```

- **`Apply(...)` names its patches explicitly**: one or more, applied in order, relative to the
  sample's acceptance folder, read from the source tree (not copied to the build output). A
  missing patch, or one `git apply` rejects, fails the test naming the file. Naming the patch
  after the method was rejected: it cannot express a shared or combined patch.
- **`Expect(...)` takes one `Expected` record.**
  - **Required**: `Because`, `Outcome`, `Selected`, `RunInFull`.
  - **Optional, exact when given**: `WillRun` (count per project key, as the schema has it),
    `Notices` (the set of codes), `Changes` (member display → test count), `Tiers` (member
    display → tier). Left out means not checked.
  - One assertion compares every given section and reports **all** the differences, section by
    section, with `Because` printed at the top.
  - **Determinism is checked inside `Expect`** (two byte-identical `report.json` files), never
    written in a test.
  - Chained calls (`.Selects(...).RunsInFull(...)`) were rejected: "required" would become a
    convention rather than something the compiler checks.
- **`Because` is required.** One sentence: why exactly these tests. It answers the prototype's
  rename-and-delete test, where nothing in the patch says why `Net_is_the_sum_of_the_lines` is
  selected.
- **Keys.** `Selected` and `RunInFull` are keyed by **test-project name** (the `.csproj` file
  name without extension). A multi-targeted project's entries are written **`Name@tfm`**. A bare
  name matching more than one `report.json` entry is an ambiguity error, so a framework cannot
  be skipped by accident. A project not listed has nothing selected. Tests are listed by full
  `display` name.
- **Shared expected-result pieces** are ordinary C# static members.

### Known gaps

**Both a `[KnownGap]` attribute** (an xUnit v3 trait, `Batch=known-gap`) **and a method name
starting `Known_gap_`.** The name is what a human reads; the trait is what the release gate
excludes. A guard test fails where the two disagree.

### Not changed

No `CONTEXT.md` change and no ADR: these are test-infrastructure decisions, and the vocabulary
lives in the map's Notes.

### Passed on

- A guard against a patch no test uses → [Writing a patch and keeping it applying](08-writing-a-patch-and-keeping-it-applying.md).

## Comments

- **Amended by [The small sample solutions](10-the-small-sample-solutions.md):** a test may name **one baseline patch**, applied before the baseline
  commit, ahead of its change patches — by sketch
  `Baseline("no-global-json.patch").Apply("x.patch").Expect(...)`. Baseline patches sit flat
  beside the change patches in `Acceptance/<Name>/`. The exact spelling is the spec's.
- **Amended by [Migrating the Contoso tests](06-migrating-the-contoso-tests.md):** besides
  determinism, `Expect` checks two invariants on every test. A project with nothing selected that
  does not run in full has mode `skip` and zero invocations. A project run in full serialises
  `"total": "unknown"`, never `0`.
