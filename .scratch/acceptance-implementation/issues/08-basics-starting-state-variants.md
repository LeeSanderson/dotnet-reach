# Basics: starting-state variants

Status: resolved
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

## Comments

- **Done.** Rows 9-11 in `Acceptance/Basics/BasicsTests.cs`, each a baseline patch plus a change
  patch, all saved with `patch.cs` (`save`, then `apply` the baseline and `save --on` it):
  `multi-targeted` + `greeting-hello-change` (row 9), `platform-suffixed` + `mileage-change`
  (row 10), `no-global-json` + `vowels-change` (row 11). Each row has its own type and test class:
  `Greeting`, `Mileage`, `Vowels` (README table updated; the four new sample tests are in
  `EveryBasicsTest`). Every other Basics test stays single `net10.0`; the variants exist only in the
  baseline patches. The generator project (ticket 07) is not on this branch, so nothing else was
  retargeted. The baseline patches touch only the two `.csproj` files, so they should merge with it
  unchanged.
- Deleted `A_multi_targeted_project_contributes_an_entry_per_framework` and
  `A_project_on_a_framework_needing_the_runner_setting_says_so` from `AcceptanceTests.cs`.
- **Checked by hand once** (a temporary dump of the report, since removed):
  - Row 9: two entries, each with one invocation carrying the matching `-f net8.0` / `-f net10.0`.
    `WillRun` is 1 on each, which shows nothing `Selected` does not, so it is not given.
  - Row 10: both entries `run-all`, keys `Basics.Tests@net10.0` and `Basics.Tests@net10.0-windows`.
    The first carries the one project-wide invocation with no `-f`; the second has none. The
    selected test is still listed on both, and `total` is **known (24)**, with `willRun` 24.
  - Row 11: removing `global.json` changes only the notice: same selection, same invocation shape.
    `global.json` holds nothing else, as spec §2.2 requires.
- **Harness change (owner-approved): "unknown is never zero" narrowed to spec §4.4's intent.**
  Row 10 fails the literal check (every `run-all` entry must serialise `"unknown"`), because a
  platform-suffixed project is analysed and only rendered project-wide, so its total is known. The
  invariant now fails when an uncounted total (`Total` null) serialises as anything but
  `"unknown"`, or a `run-all` entry serialises a total of `0`. `ExpectationTests` renames the zero
  case and adds one where a run-in-full project carries a counted total.
- **Full suite:** 656 passed, 0 failed (Windows, Debug).
- No Reach-vs-spec findings: all three rows matched the hand-written expectations on the first run.
- The worktree had no Husky.Net hooks installed; `dotnet tool restore` and `dotnet husky install`
  fixed the pre-commit hook. Linux not run locally.
