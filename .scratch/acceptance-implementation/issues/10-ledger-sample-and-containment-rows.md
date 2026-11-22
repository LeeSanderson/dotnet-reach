# Ledger: the sample and its containment rows

Status: ready-for-agent
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
