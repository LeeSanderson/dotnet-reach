# Acceptance layer implementation

The build effort for Reach's acceptance layer. Planning is done. This directory holds only work
that produces code.

**The specification is [`.scratch/acceptance-layer/spec.md`](../acceptance-layer/spec.md).**
Every ticket cites the sections it implements. Where a ticket and the spec disagree, the spec wins
and the ticket has a bug.

Do **not** treat [`.scratch/acceptance-layer/`](../acceptance-layer/) as a backlog. Its `issues/`
directory holds the decision tickets that produced the spec, all resolved. Zoom into one when a
ticket here tells you to.

## Tickets

Numbered in landing order. Each is sized to one agent session. **Stage order is integration order,
not release order**: nothing is released until ticket 16, and then only by
[Publishing](../m1-implementation/issues/21-publishing.md), which depends on it.

**Stage A: the harness.** At the end of it, one acceptance test runs on the new harness over
Basics.

| # | Ticket | Depends on |
|---|---|---|
| 01 | [Test running: full parallelism and the two CI steps](issues/01-test-running-parallelism-and-ci-steps.md) | — |
| 02 | [The harness, and Basics' first row](issues/02-the-harness-and-basics-first-row.md) | 01 |
| 03 | [The patch tool and the stripped-form guard](issues/03-the-patch-tool-and-the-stripped-form-guard.md) | 02 |

**Stage B: what the gate needs from Reach and its docs.** Independent of stage A; can run in
parallel with it.

| # | Ticket | Depends on |
|---|---|---|
| 04 | [Emit `recompilation-widening` and `langversion-above-ceiling`](issues/04-emit-the-two-missing-notices.md) | — |
| 05 | [Cut and cite: what the docs stop claiming](issues/05-cut-and-cite.md) | — |

**Stage C: the samples and their rows.** 06–10 can run in parallel once 03 has landed.

| # | Ticket | Depends on |
|---|---|---|
| 06 | [Basics: selection and tier rows](issues/06-basics-selection-and-tier-rows.md) | 03 |
| 07 | [Basics: outcome and reason rows](issues/07-basics-outcome-and-reason-rows.md) | 03 |
| 08 | [Basics: starting-state variants](issues/08-basics-starting-state-variants.md) | 03 |
| 09 | [Basics: known gaps and the known-gap guard](issues/09-basics-known-gaps.md) | 03 |
| 10 | [Ledger: the sample and its containment rows](issues/10-ledger-sample-and-containment-rows.md) | 03 |
| 11 | [Ledger: recompilation rows and known gaps](issues/11-ledger-recompilation-rows-and-known-gaps.md) | 04, 09, 10 |
| 12 | [Frameworks: rows 7 and 8, the example report, and the end of `tests/fixtures/`](issues/12-frameworks-and-the-end-of-fixtures.md) | 06, 07, 08, 11 |

**Stage D: the rest of the gate.** 13 and 14 are independent of everything above.

| # | Ticket | Depends on |
|---|---|---|
| 13 | [Exit-path CLI tests](issues/13-exit-path-cli-tests.md) | — |
| 14 | [Report-writer and unit-level tests](issues/14-report-writer-and-unit-tests.md) | — |
| 15 | [Parity tests: the notice index, the walk-through and the help](issues/15-parity-tests.md) | 04, 05, 12 |
| 16 | [The gate audit](issues/16-the-gate-audit.md) | all |

## One hard ordering constraint

**No old acceptance test is deleted before its replacement lands** (spec §1.3). Each ticket that
lands a row deletes the old Contoso test that row replaces, in the same change, and no earlier.
Ticket 12 deletes what is left, and it depends on every ticket whose rows replace an old test.

## Standing rules for every ticket

1. **Expected results are written by hand**, never copied from Reach's output (spec §1.3). If you
   catch yourself pasting a report into an `Expected`, stop: the point is that a human said what
   *should* be selected.
2. **Reach's output is not changed to suit a test.** Where Reach contradicts its docs, report the
   finding in the ticket's comments and pin what Reach does. Do not reshape the report.
3. **Every sample builds clean in `Reach.slnx` under the coding standard** (spec §2.1). No
   suppression without the maintainer's per-rule approval (see
   [coding-standards](../coding-standards/spec.md)).
4. **Each row's patch touches only types no other row's tests reach** (spec §2.2).
5. **A patch is written with `tests/tools/patch.cs`** once ticket 03 has landed, never by hand.
