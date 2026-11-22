# Ledger: recompilation rows and known gaps

Status: ready-for-agent
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
