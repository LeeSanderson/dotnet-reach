# Ledger: the sample and its containment rows

Status: resolved
Depends on: 03
Spec: [§2.5](../../acceptance-layer/spec.md#25-ledger), [§7.2](../../acceptance-layer/spec.md#72-release-gate-the-multi-project-sample) rows 17, 20–23

## Goal

The multi-project sample, and the five gate rows about what is **not** selected that do not need
recompilation widening.

## Scope

**The sample** at `tests/samples/Ledger/`, exactly the diamond in spec §2.5: four production
projects, four `xunit.v3` test projects (at a version raising no notice), single `net10.0`,
`src/Collections/Directory.Build.props` covering `Ledger.Dunning` only, `global.json` with the
runner setting, and a README listing the gap-shaped members (`IReminderChannel` and its
implementers, `Money.DefaultCurrency`).

- Twelve sample tests, three per test project, with `Ledger.Api.Tests` split as the spec says
  (posting → Billing only, reminders → Dunning only, health → neither).
- At least one Domain test touches `Invoice.DaysOverdue`, which only Dunning calls.
- Build the gap shapes now, so ticket 11 only writes tests: `IReminderChannel` in Domain,
  `EmailChannel` in Dunning, `WebhookChannel` in Api, `ReminderSender` calling through the
  interface; `Money.DefaultCurrency` as a `const`; `TaxRules.StandardRatePercent` in Billing;
  `ReminderSchedule.Next(DateOnly)` as an unused overload in Dunning.
- All eight projects go in `Reach.slnx` under `/samples/` and build clean.

**Rows**, one acceptance test each, in `Acceptance/Ledger/LedgerTests.cs`:

| Row | Patch | Notes |
|---|---|---|
| 17 | body of `Invoice.DaysOverdue` | — |
| 20 | a harmless `<NoWarn>` in `Ledger.Billing.csproj` | shares a static `Expected` piece with row 22 |
| 21 | `src/Collections/Directory.Build.props` | — |
| 22 | move a Billing type to another namespace | `Tiers` = `whole-assembly`; same selection as row 20 |
| 23 | add or change `CODEOWNERS` at the root | `Notices`. **Pin what Reach does** (spec §7.2) |

Each `Because` says what is left out and why.

**Row 23 finding.** If Reach reports `no-changes` rather than `nothing-selected` with
`unmapped-file-no-project`, pin `no-changes`. Then record in this ticket's comments that the docs'
claim (the register's row-6 entry and the schema) contradicts Reach. Ticket 16 decides whether the
docs or Reach changes before the alpha. Do not change Reach here.

## Acceptance criteria

- Ledger builds clean in `Reach.slnx`. Each test takes about 6 s or less on a warm machine (the
  cost research measured 6.17 s); report the real figure in this ticket's comments.
- Five acceptance tests pass on Windows and Linux, each with an exact selection that leaves out
  what the spec's "Not selected" column lists.
- No Ledger test raises a notice it does not mean. Rows 17, 20, 21 and 22 raise none.

## Out of scope

Rows 18, 19, 26, 27 (ticket 11).

## Comments

- **Done.** `tests/samples/Ledger/` is the spec §2.5 diamond: Domain (`Invoice`, `Money`,
  `IReminderChannel`), Billing (`TaxRules`, `InvoicePoster`, `InvoiceNumbering`, `PostedInvoice`),
  Dunning under `src/Collections/` (`ReminderSchedule`, `OverdueInvoices`, `ReminderSender`,
  `EmailChannel`), and Api (`PostingEndpoint`, `RemindersEndpoint`, `HealthEndpoint`,
  `WebhookChannel`). It has four `xunit.v3` 4.0.0 test projects with three tests each, `global.json`
  with the runner setting, and a README listing what each row changes and the gap-shaped members.
  All eight projects are in `Reach.slnx` under `/samples/`. The Release build and
  `dotnet format --verify-no-changes` are clean. Ticket 11's shapes are in place:
  `TaxRules.StandardRatePercent`, the unused `ReminderSchedule.Next(DateOnly)`, the interface
  fan-out and `Money.DefaultCurrency`.
- **Rows** are in `Acceptance/Ledger/LedgerTests.cs`. Each has its patch, saved with the tool:
  17 `days-overdue-body-change`, 20 `billing-project-nowarn`, 21 `collections-build-props`, 22
  `invoice-numbering-namespace-move` (the type moves to `Ledger.Billing.Numbering`, and
  `InvoicePoster.cs` gains its `using`), and 23 `codeowners-added`. Rows 20 and 22 share
  `BillingWidenedAlone`. Rows 17, 20, 21 and 22 give `Notices` as just the two standing codes,
  which proves they raise nothing else. No old Contoso test is replaced by these rows (spec §8.1),
  so none was deleted.
- **Row 23: no finding.** Reach reports `nothing-selected` with `unmapped-file-no-project`, as the
  docs say. `CODEOWNERS` is an unmappable path, so the change set is not empty, and
  `ReportBuilder.OutcomeOf` does not return `no-changes`.
- **Row 22's `Tiers` include a field at `whole-assembly`.** Under the new name, every member is
  added. `Next()` joins at `member`. The private field `_last` has no method to join, so it takes
  `RootSets.ChangesFrom`'s fall-back ("the declaration could not be matched to compiled code",
  `whole-assembly`). This is deliberate over-selection, and it widens the assembly the deleted
  type already widened, so the selection is unchanged. I pinned it. Note for ticket 11: any patch
  that adds or changes a field gets a `whole-assembly` entry for it.
- **Design changes from analyzer findings, none suppressed.** Sonar S2339 rejects a `public
  const`, so `Money.DefaultCurrency` and `TaxRules.StandardRatePercent` are `internal const`.
  Reach's recompilation widening fires on any changed compile-time constant whatever its
  visibility, so rows 18 and 27 still hold. The README and the doc comments say why. S3400
  rejected a method that returns a constant, so `HealthEndpoint.Check` takes `storeReachable`.
- **`src/Collections/Directory.Build.props` imports the props above it** when there are any
  (`GetPathOfFileAbove`). Otherwise it would hide the repository's props from `Ledger.Dunning`
  in place, which turns off the coding standard and warnings-as-errors. In the harness's copy
  there is nothing above it, so the import is skipped.
- **Full suite:** 641 passed, 0 failed (Windows, Debug).
- **Timing (Windows, Debug, warm, machine heavily loaded by parallel sessions):** one Ledger test
  alone took 10.3 s and 12.6 s. All five in parallel took 42 s. For comparison, Basics' row 1 took
  33 s and 49 s on the same machine in the same session, so these figures say more about the load
  than about Ledger. The 6 s criterion cannot be confirmed here. The CI timing will be the real
  figure.
- Linux not run locally. The patches are CRLF from the tool, as ticket 03 describes.
- The worktree had no `.husky/_/`, so the pre-commit hook could not run. I ran
  `dotnet husky install`, which sets the same relative `core.hooksPath`. I did not skip the hook.
