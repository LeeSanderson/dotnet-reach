# Reshape Ledger so row 18 selects what the spec says

Status: ready-for-agent
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
