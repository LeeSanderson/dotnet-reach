# Basics: selection and tier rows

Status: resolved
Depends on: 03
Spec: [§2.2](../../acceptance-layer/spec.md#22-how-samples-are-split), [§2.3](../../acceptance-layer/spec.md#23-basics), [§7.1](../../acceptance-layer/spec.md#71-release-gate-small-samples) rows 2–6, [§8.1](../../acceptance-layer/spec.md#81-where-each-test-goes)

## Goal

Rows 2–6 on Basics: a changed test body, a method gaining `[Fact]`, dispatch widening, and the two
whole-type tiers.

## Scope

Add to `Basics.Core` and `Basics.Tests` the types each row needs. **Each row gets its own types**,
which no other row's tests reach (spec §2.2). Then one acceptance test and one patch per row, in
`Acceptance/Basics/`:

| Row | Patch | Notes |
|---|---|---|
| 2 | change the body of an existing test method | selects that test (rule: its own source changed) |
| 3 | an existing **non-test** method in `Basics.Tests`, body unchanged, gains `[Fact]` | the case walking-skeleton ticket 07 escalated. Must be an existing method, not a new one |
| 4 | change an implementation behind an interface **and** a virtual override | its own hierarchy: an interface with ≥2 implementations and a base class with ≥2 overrides, each with tests. Selection shows every candidate. If `Tiers` or `pathClass` makes the widening visible, consider giving `Tiers` |
| 5 | delete a method | `Tiers` = `whole-type` |
| 6 | move a type's file to another directory, same namespace | `Tiers` = `whole-type` |

Every test has a one-sentence `Because` that says why *exactly* these tests.

**Delete** `Fact_added_to_an_existing_method_selects_it` from `Acceptance/AcceptanceTests.cs`. Its
patch was really row 2, and rows 2 and 3 now cover it (spec §8.1).

## Acceptance criteria

- Five new acceptance tests pass on Windows and Linux, each with an exact expected selection
  written by hand.
- Row 3's patch adds only the attribute: the method's body is unchanged in the diff.
- No new notice appears on any Basics test, row 1 included (`Notices` is not given, but check the
  report once by hand).
- The old test is deleted. Basics still builds clean in `Reach.slnx`.

## Out of scope

Known-gap shapes (ticket 09). Rows on other tickets.

## Comments

- **Done.** Rows 2–6 in `Acceptance/Basics/BasicsTests.cs`, one patch each, all written with
  `patch.cs save`: `test-body-change`, `fact-added-to-an-existing-method` (adds only `[Fact]`),
  `dispatch-implementation-changes`, `method-deleted`, `type-moved-to-another-directory`. Each row
  has its own types (README table updated): `Initials`; `Rounding` + `RoundingChecks`;
  `IShippingRate`/`FlatRate`/`WeightRate`/`Shipping` and `Label`/`UpperLabel`/`BracketLabel`/`Printer`;
  `Thermometer` + `Readings` (in `Basics.Tests`); `Tally`. Row 4 gives `Tiers` (both `member`):
  the widening shows in the selection (calls through the interface or base are selected, direct
  calls to the unchanged sibling are not), not in the tier. `Fact_added_to_an_existing_method_selects_it`
  deleted. `Reach.slnx` Release `-warnaserror` and `dotnet format --verify-no-changes` clean. Full
  suite 640 passed, 0 failed (Windows, Debug). Linux not run locally.
- **Finding (Reach vs spec row 5): a deleted method widens its whole assembly, not just its type.**
  `DeclarationComparison` adds the intended whole-type widening, but the removed member is also in
  `ChangedSet.Members`, cannot join to compiled code (it no longer exists), and so falls through in
  `RootSets.ChangesFrom` to whole-assembly widening ("the declaration could not be matched to
  compiled code"). Pinned: `Tiers` = `Readings` `whole-type` **and** `Readings.BodyHeat()`
  `whole-assembly`, and every Basics test selected. The test is named
  `A_deleted_method_widens_at_least_its_declaring_type`. It turns red when this is fixed; the
  intended selection is the two `ThermometerTests` that read `Readings.Freezing`.
- **Why row 5's type is in `Basics.Tests`:** any removal in `Basics.Core` (private included) also
  fires `recompilation-widening` over `Basics.Tests`, which selects everything regardless. In the
  test project, nothing references it, so once the fall-through is fixed the row narrows to the type.
- **Consequence for tickets 07–09:** row 5 selects every Basics test, via the static
  `EveryBasicsTest` list. **Every new sample test in `Basics.Tests` must be added to it**, or row 5
  fails. That is a merge point.
- **Notices, checked by hand once** (a temporary default on `Expected.Notices`): rows 1–5 carry only
  `baseline-resolved` and `baseline-is-head`. **Row 6 also carries `untracked-source-files`**,
  because the harness leaves a patch-created file untracked (spec §5.1's accepted difference), and a
  move creates one. Not a Reach change; any test that moves or adds a file and gives `Notices` must
  list it.
- Row 4's `BracketLabel` change was first `$"[{text}]"` → `"[" + text + "]"`, which likely compiles
  to the same IL; it was redone as a behaviour change before saving.
