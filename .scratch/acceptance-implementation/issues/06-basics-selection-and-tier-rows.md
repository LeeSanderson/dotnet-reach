# Basics: selection and tier rows

Status: ready-for-agent
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
