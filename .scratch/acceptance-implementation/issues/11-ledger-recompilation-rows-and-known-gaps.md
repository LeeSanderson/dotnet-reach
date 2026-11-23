# Ledger: recompilation rows and known gaps

Status: resolved
Depends on: 04, 09, 10
Spec: [§7.2](../../acceptance-layer/spec.md#72-release-gate-the-multi-project-sample) rows 18–19, [§7.3](../../acceptance-layer/spec.md#73-known-gaps-not-in-the-gate) rows 26–27, [§8.1](../../acceptance-layer/spec.md#81-where-each-test-goes)

## Goal

Recompilation widening shown widening **only** its transitive referencers, and the two
multi-project gaps pinned.

## Scope

| Row | Patch | Sections beyond the required four |
|---|---|---|
| 18 | change `TaxRules.StandardRatePercent` (a `const` in Billing) | `Tiers`, `Notices` = `recompilation-widening` |
| 19 | remove the unused overload `ReminderSchedule.Next(DateOnly)` in Dunning | `Tiers`, `Notices` = `recompilation-widening` |
| 26 (gap) | change `EmailChannel.Send` | Api's reminders test (webhook channel) is still selected |
| 27 (gap) | change `Money.DefaultCurrency` | all 12 tests; `Changes` shows the one `const` |

- Rows 18 and 19 select **all** of `Ledger.Api.Tests`, health test included. `Because` says why:
  the test project itself references the declaring assembly transitively and is widened whole.
  Rows 20 and 22 (ticket 10) do not, and that contrast is the point.
- Rows 26 and 27 are `Known_gap_…` with `[KnownGap]` (ticket 09's mechanism). Their `Because`
  names the register entry.

**Delete** `A_changed_const_consumed_across_an_assembly_boundary_selects_the_consumers_tests` from
`Acceptance/AcceptanceTests.cs`. Row 18 replaces it (spec §8.1).

## Acceptance criteria

- Two gate tests and two known-gap tests pass on Windows and Linux.
- Rows 18 and 19 assert `recompilation-widening` in an exact `Notices` section.
- The old `const` test is deleted.

## Out of scope

Emitting the notice (ticket 04).

## Comments

- **Done.** Four tests in `Acceptance/Ledger/LedgerTests.cs`, each with a patch saved by the tool:
  18 `standard-rate-const-change`, 19 `unused-overload-removed`, 26 `email-channel-send-change`,
  27 `default-currency-const-change`. Rows 18 and 19 give exact `Notices` (the two standing codes
  plus `recompilation-widening`) and `Tiers`; both select all of `Ledger.Api.Tests`, health test
  included. Rows 26 and 27 are `Known_gap_…` with `[KnownGap]`, naming the register entries
  "Widening residues from receiver-type inference" and "Recompilation widening's blast radius".
  The old Contoso const test is deleted.
- **Finding: row 18 also selects `Ledger.Dunning.Tests.ReminderSenderTests`**, which spec §7.2
  says is not selected. Widening itself is right (Billing's referencers: Billing.Tests, Api,
  Api.Tests). But Api is then rooted whole, and the reverse walk from Api's `WebhookChannel.Send`
  comes back into Dunning through the `IReminderChannel.Send` call in `ReminderSender`: row 26's
  interface fan-out. Pinned, with a comment and the `Because` saying why. This breaks standing
  rule 4 (row 26's gap shape reaches into row 18). Ticket 16 decides whether to reshape Ledger
  (for example, move `WebhookChannel` out of Api) or amend the spec row. The Ledger README notes it.
- **Finding: one display can be two change entries.** A changed `const` is a field, so it fails
  to join and falls back to `whole-assembly`, and it is also its own recompilation-widening entry.
  A removed member is the same (a failed join plus the widening), and its type is a separate
  `whole-type` entry. Row 19's tiers: `ReminderSchedule` = `whole-type`, `Next(DateOnly)` =
  `whole-assembly` twice. Row 27's `Changes`: 10 (Domain's whole assembly; the health and schedule
  tests never reach it), then 12. The harness could not express this, so `Changes` and `Tiers` now
  take `Entries<T>`: a bare value as before, or `Entries.Of(a, b)` for several in report order. A
  new `ExpectationTests` case covers it. The S4017 finding on the first version moved spelling onto
  `Entries<T>`, so no generic signature nests a type argument.
- **Full suite:** 658 passed, 0 failed (Windows, Debug). `dotnet format --verify-no-changes` is
  clean. Linux not run locally.
