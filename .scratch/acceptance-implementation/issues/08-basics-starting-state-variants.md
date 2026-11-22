# Basics: starting-state variants

Status: ready-for-agent
Depends on: 03
Spec: [§3.3](../../acceptance-layer/spec.md#33-baseline-patches), [§7.1](../../acceptance-layer/spec.md#71-release-gate-small-samples) rows 9–11, [§8.1](../../acceptance-layer/spec.md#81-where-each-test-goes)

## Goal

The three rows whose starting state differs from Basics': multi-targeting, a platform suffix, and
a missing runner setting. Each is a baseline patch plus a change patch.

## Scope

Write each baseline patch with `patch.cs save`, and each change patch with `save --on <baseline>`.

| Row | Baseline patch | Change patch | Sections beyond the required four |
|---|---|---|---|
| 9 | retarget `Basics.Core` **and** `Basics.Tests` to `net8.0;net10.0` | a body change | keys `Basics.Tests@net8.0` and `Basics.Tests@net10.0` |
| 10 | retarget the same two to `net10.0;net10.0-windows`, with `<EnableWindowsTargeting>true</EnableWindowsTargeting>` | a body change | `Notices` = `framework-selector-underivable`; `RunInFull` |
| 11 | delete `global.json` | any one-line body change | `Notices` = `test-runner-not-configured` |

- Row 9 also needs the invocations pinned with `-f`. `Selected` keys prove one entry per framework;
  check by hand once that each invocation carries a matching `-f`, and give `WillRun` per key if it
  makes the pinning visible.
- Row 10 must build on the **Linux** leg. That is what `EnableWindowsTargeting` is for (`NETSDK1100`
  otherwise).
- Row 11: if deleting `global.json` changes anything besides the notice, `global.json` holds
  something it should not (spec §2.2). Fix the sample, not the test.
- If the generator project (ticket 07) is affected by the retargeting, retarget only what the row
  needs and record why.

**Delete from `Acceptance/AcceptanceTests.cs`**, each with its replacement:
`A_multi_targeted_project_contributes_an_entry_per_framework` (row 9) and
`A_project_on_a_framework_needing_the_runner_setting_says_so` (row 11).

## Acceptance criteria

- Three new acceptance tests pass on Windows **and Linux**.
- No other Basics test pays for the extra framework builds. The variants exist only in the
  baseline patches.
- The two old tests are deleted.

## Out of scope

Removing `Contoso.Core`'s multi-targeting (ticket 12).
