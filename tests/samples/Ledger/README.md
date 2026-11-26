# Ledger

The multi-project **sample solution** for Reach's acceptance layer: a small invoicing and
collections back office. Every row it carries is about what is **not** selected, so it is shaped
to have tests to leave out. See the acceptance-layer spec, §2.5 and §7.2.

This folder is the baseline repository Reach reads in an acceptance test, so it holds the sample
and nothing else. The acceptance tests live in `tests/Reach.Tests/Acceptance/Ledger/`, beside the
patches they apply.

```
src/Ledger.Domain                Invoice, Money, IReminderChannel
src/Ledger.Billing               -> Domain
src/Collections/Ledger.Dunning   -> Domain   (under src/Collections/Directory.Build.props)
src/Ledger.Webhooks              -> Domain
src/Ledger.Api                   -> Billing, Dunning, Webhooks
tests/Ledger.Domain.Tests        -> Domain
tests/Ledger.Billing.Tests       -> Billing
tests/Ledger.Dunning.Tests       -> Dunning
tests/Ledger.Api.Tests           -> Api
```

- **Billing and Dunning are siblings.** Neither uses the other.
- **Ledger.Webhooks holds only WebhookChannel, and has no test project.** Api's reminders
  test uses it. It is apart from Api so that no assembly a gate row widens whole holds an
  IReminderChannel implementer (see below).
- Every test project is `xunit.v3` 4.0.0, and `global.json` holds the testing-platform runner
  setting and nothing else that changes the build, so no run raises a standing notice.
- Everything targets a single `net10.0`.
- `src/Collections/Directory.Build.props` covers `Ledger.Dunning` only. `tests/` stays flat, so
  a change to it reaches `Ledger.Dunning.Tests` only by the reverse walk.

## The sample tests

Three per test project. `Ledger.Api.Tests` is split on purpose, one test per endpoint:

| Test | Reaches |
|---|---|
| `PostingTests.Posting_an_invoice_returns_its_total` | Billing only |
| `RemindersTests.Requesting_reminders_lists_the_overdue_invoices` | Dunning only |
| `HealthTests.Health_check_reports_ok` | neither |

`Invoice.DaysOverdue` is called only by Dunning, and by one Domain test.

## What each row changes

| Row | Member or file |
|---|---|
| 17 | the body of `Invoice.DaysOverdue` |
| 18 | `TaxRules.StandardRatePercent`, a `const` in Billing |
| 19 | `ReminderSchedule.Next(DateOnly)`, an unused overload in Dunning, removed |
| 20 | `src/Ledger.Billing/Ledger.Billing.csproj` |
| 21 | `src/Collections/Directory.Build.props` |
| 22 | `InvoiceNumbering`, moved to another namespace |
| 23 | `CODEOWNERS`, a root file matching no rule |

## Gap-shaped members

Known-gap tests depend on these. Gate patches never touch them.

| Member | Known gap |
|---|---|
| `IReminderChannel` (Domain), `EmailChannel` (Dunning), `WebhookChannel` (Webhooks), called through the interface by Dunning's `ReminderSender` | 26: interface fan-out |
| `Money.DefaultCurrency` (Domain), a `const` | 27: a core `const`'s blast radius |

`WebhookChannel` has a project of its own so that row 26's shape stays out of rows 18 and 19,
which widen Api whole. Were it in Api, the reverse walk from its `Send` would come back into
Dunning through `ReminderSender`'s interface call, and row 18 would select Dunning's
`ReminderSenderTests`. Nothing widens Webhooks whole except a Domain change (row 27).

The two `const`s are `internal`, not `public`: a public constant trips Sonar S2339, and Reach
widens on a changed compile-time constant whatever its visibility.

## Building it

The projects are in `Reach.slnx` under `/samples/`, so the main build holds them to the coding
standard. `src/Collections/Directory.Build.props` imports the repository's props when there are
any above it, so `Ledger.Dunning` is held to it too. Their tests are never run by `dotnet test`;
they run only through Reach, inside an acceptance test, over a copy of the files git tracks here.
