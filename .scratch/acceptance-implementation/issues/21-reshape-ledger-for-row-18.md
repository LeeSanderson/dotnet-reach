# Reshape Ledger so row 18 selects what the spec says

Status: resolved
Depends on: 11, 16
Spec: [§7.2](../../acceptance-layer/spec.md#72-release-gate-the-multi-project-sample) row 18, [§2.2](../../acceptance-layer/spec.md#22-how-samples-are-split)

## Goal

Row 18 selects exactly what spec §7.2 says, and no other row's tests reach its types. That
restores standing rule 4.

## Background

Found by ticket 11. The owner decided on 2026-11-24 to reshape the sample rather than amend the
spec. Row 18 also selects Dunning's `ReminderSenderTests`. Recompilation widening stops at
Billing's referencers, but it widens Api as a whole, and the reverse walk then returns into Dunning
through `IReminderChannel.Send`. That's the shape known gap 26 pins. Ticket 16 recommended moving
`WebhookChannel` out of Api into its own project. Read tickets 11 and 16's `## Comments`, and
[the Ledger design](../../acceptance-layer/issues/05-the-first-multi-project-sample-solution.md).

## Scope

1. Reshape Ledger so that Api's whole-assembly widening no longer reaches into Dunning through the
   channel interface. Start from ticket 16's suggestion, and keep it if it works. A new project
   joins `Ledger.slnx` and `Reach.slnx` under `/samples/`, and builds clean under the coding
   standard.
2. Row 26, the interface fan-out known gap, must still pin its gap. Its expected result may change
   with the reshape. Work out the new result by hand.
3. Re-check every Ledger row: 17–23, 26 and 27. Patches that no longer apply are re-saved with
   `patch.cs` (or remade by hand and saved). Any expected result that changes is worked out by
   hand, and the change is explained in the comments.
4. Update the Ledger design notes (sample README, spec §2.5 if it lists projects) and spec §9.2's
   test names, if any change.

## Acceptance criteria

- Row 18 selects exactly spec §7.2's tests.
- No Ledger row's tests reach another row's patched types, except the known gaps that exist to
  show it.
- The full suite and the release-gate run are green, and the Release `-warnaserror` build and
  format check are clean.

## Comments

**2026-11-24, implementation.** Kept ticket 16's suggestion: `WebhookChannel` moved out of Api
into a new project, `src/Ledger.Webhooks` (→ Domain, no test project), which Api references and
which joins `Ledger.slnx` and `Reach.slnx`. Api now holds no `IReminderChannel` implementer, so
widening it whole no longer fans back into Dunning through `ReminderSender`'s interface call.

Expected results, re-worked by hand for every Ledger row:

- **Row 18 changed.** It drops `Ledger.Dunning.Tests.ReminderSenderTests.A_reminder_names_the_days_overdue`
  and now selects exactly spec §7.2: all of Billing.Tests and all of Api.Tests. Billing's
  referencers are Billing.Tests, Api and Api.Tests; Webhooks does not reference Billing, and
  nothing widened whole holds an `IReminderChannel.Send` body any more.
- **Rows 17, 19, 20, 21, 22, 23 unchanged.** None of their patches touches Api or the channel;
  Webhooks references neither Billing nor Dunning, so no recompilation widening reaches it, and it
  has no tests of its own. Row 19 widens Api whole too, and selects what it did.
- **Row 26 unchanged**, and still pins its gap: `EmailChannel.Send` reaches the callers of
  `IReminderChannel.Send`, so Dunning's `ReminderSenderTests` and Api's reminders test (which only
  sends by webhook) are both selected.
- **Row 27 unchanged** (10, then 12): Webhooks references Domain and widens with it, but has no
  tests and adds no path that was not already there.

No patch needed re-saving: none touches the moved file or `RemindersEndpoint.cs`. Spec §2.5, §7.2's
note on row 18 and §9.2's row 18 entry (the "open" marker removed), and the Ledger README are
updated. Test names are unchanged.
