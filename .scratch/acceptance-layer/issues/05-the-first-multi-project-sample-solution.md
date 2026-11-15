# The first multi-project sample solution

Type: grilling
Status: resolved
Blocked by: (none — 03 resolved)

## Question

Design the first sample solution shaped like a real codebase: its layers and projects, its
test projects and which framework each uses, and — the reason it exists — which test projects
each planned patch must **not** select.

Driven by the capabilities [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md)
assigns to multi-project sample solutions. Bounded by the build cost
[What does a built, patched sample solution cost?](02-building-a-patched-sample-cheaply.md)
measures, if that has resolved.

## Comments

- **From [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md):** this sample carries gate rows **17–23** (cross-assembly edge, `const` and
  removed-member recompilation widening, `.csproj` and `Directory.Build.props` containment,
  namespace move, unmapped root file) and known-gap rows **26–27** (interface-typed receiver
  fan-out, a core `const`'s blast radius). Every gate row is about what is *not* selected. Exact
  notice sections mean the sample must not trip notices it does not intend.

## Answer

Settled with the owner over two rounds of grilling, against spec §9.3–§9.4 and §10.1. The
facts that shape it: recompilation widening is whole-assembly widening of every project that
**transitively references** the declaring one, test projects included; rule-table rows never add
dependents, which are reached only by the reverse walk from the widened assembly.

### The sample: Ledger

A small invoicing and collections back office, at `tests/samples/Ledger/`. Four production
projects in a **diamond**, one test project each — 8 projects, the size the cost research
measured at ~6 s per test.

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

- Billing and Dunning are **siblings**: neither uses the other. That is what gives every row
  something to leave out.
- **All test projects are xunit v3**, at a version that raises no notice. **Everything targets a
  single `net10.0`.** Dialects, multi-targeting and platform suffixes belong to the small
  samples; here they would only add notices and `Name@tfm` keys.
- `tests/` stays flat: the `Collections` props covers `Ledger.Dunning` only, so row 21 shows
  containment *and* the reverse walk reaching a test project outside it.

### The sample tests

**Three per test project, twelve in all.** `Ledger.Api.Tests` is split on purpose:

- `Posting_an_invoice_returns_its_total` — reaches Billing only;
- `Requesting_reminders_lists_the_overdue_invoices` — reaches Dunning only;
- `Health_check_reports_ok` — reaches neither.

At least one Domain test touches `Invoice.DaysOverdue`, which only Dunning calls.

### The patches

| # | Patch | Selected | **Not selected** |
|---|---|---|---|
| 17 | Body of `Invoice.DaysOverdue` (Domain), called only by Dunning | its Domain test, the Dunning tests reaching it, Api's reminders test | Billing.Tests, the other Domain tests, Api's posting and health tests |
| 18 | A `const` in **Billing** (`TaxRules.StandardRatePercent`) | **all** Billing.Tests, **all** Api.Tests | Domain.Tests, Dunning.Tests |
| 19 | Remove an unused overload in **Dunning** (`ReminderSchedule.Next(DateOnly)`) | all Dunning.Tests, all Api.Tests | Domain.Tests, Billing.Tests |
| 20 | `Ledger.Billing.csproj` (a harmless `<NoWarn>`) | Billing.Tests, Api's posting test | Domain, Dunning, Api's reminders and health tests |
| 21 | `src/Collections/Directory.Build.props` | Dunning.Tests, Api's reminders test | Domain, Billing, Api's posting and health tests |
| 22 | Move a Billing type to another namespace | as row 20 | as row 20 |
| 23 | A root file matching no rule (`CODEOWNERS`) | nothing | everything |

- Rows 18 and 19 select **all** of Api.Tests, health test included: `Ledger.Api.Tests` itself
  transitively references the declaring assembly and is widened whole. Rows 20 and 22 do not.
  The contrast is real and worth showing.
- Rows 20 and 22 share one static `Expected` piece.
- Rows 18, 19 and 22 give `Tiers` (the tier is the point); row 23 gives `Notices`.
- **Row 23's outcome is to be confirmed against Reach** when the test is written.
  `ReportBuilder.OutcomeOf` returns `no-changes` when the change set is empty, so `CODEOWNERS`
  may report `no-changes` rather than `nothing-selected` with `unmapped-file-no-project`. The
  test pins what Reach does; if that contradicts the docs' claim, it is a finding for the spec,
  not a change to Reach's output.

### Known gaps

- **26 — interface fan-out.** Domain declares `IReminderChannel`; Dunning has `EmailChannel`,
  Api has `WebhookChannel`; Dunning's `ReminderSender` calls through the interface. The patch
  changes `EmailChannel.Send`. The gap: Api's reminders test, which uses the webhook channel, is
  still selected.
- **27 — a core `const`'s blast radius.** Change `Money.DefaultCurrency` (Domain): all 12 tests
  are selected. Gives `Changes`, so the test shows the one `const` that did it.

**Gate patches stay clear of the gap shapes**: none touches `IReminderChannel`, its
implementers, or `Money.DefaultCurrency`. The sample's README lists these gap-shaped members.

### One sample is enough

Every row fits the diamond. No second multi-project sample now; a later feature that needs one
gets its own ticket.

### Not changed

No `CONTEXT.md` change and no ADR: test infrastructure, vocabulary already in the map's Notes.
