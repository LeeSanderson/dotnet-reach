# Basics: known gaps and the known-gap guard

Status: resolved
Depends on: 03
Spec: [§4.5](../../acceptance-layer/spec.md#45-known-gaps), [§7.3](../../acceptance-layer/spec.md#73-known-gaps-not-in-the-gate) rows 24–25, [§2.2](../../acceptance-layer/spec.md#22-how-samples-are-split)

## Goal

Known-gap tests become declarable and visibly separate from the gate, and the two small gaps are
pinned.

## Scope

**The mechanism**:

- a `[KnownGap]` attribute that is an xUnit v3 trait, `Batch=known-gap`;
- a guard test (outside the acceptance namespace, so it is fast) that reflects over `Reach.Tests`
  and fails when a method named `Known_gap_…` lacks the trait, or a method with the trait is not
  named `Known_gap_…`, listing each offender.

**The rows**, on Basics, each with its own types:

| Row | Shape | Patch | Pins |
|---|---|---|---|
| 24 | `Handler<T>`, used as `Handler<Foo>` by some tests and `Handler<Bar>` by others | change code reached only through `Handler<Foo>` | the users of `Handler<Bar>` **are** selected (accepted over-selection, ADR-0006) |
| 25 | a type whose `ToString` is reached only through string interpolation | change that `ToString` | the interpolating test is **not** selected (documented under-selection) |

Name them `Known_gap_…`. Their `Because` names the register entry in `docs/limitations.md` that
documents the gap.

**List the gap-shaped members** (`Handler<T>` and its users, the interpolated type) in the Basics
README, so no gate patch touches them.

## Acceptance criteria

- Both known-gap tests pass, with exact expected selections, and carry the trait and the name.
- The guard test fails on a deliberately mismatched method (tested, for example, over a type built
  for the test) and passes on the suite.
- A run filtered to exclude `Batch=known-gap` runs every acceptance test except these two. Record
  the filter command in this ticket's comments. Ticket 16 uses it.

## Out of scope

Ledger's gaps (ticket 11).

## Comments

- **Done.** `[KnownGap]` is `Acceptance/KnownGapAttribute.cs`, an `ITraitAttribute` giving
  `Batch=known-gap`. The guard is `Harness/KnownGapGuard.cs`, with `Harness/KnownGapGuardTests.cs`
  (7 tests, fast step). It scans every `[Fact]`/`[Theory]` method on a concrete class in `Reach.Tests`.
  It counts the trait from any trait attribute, so a plain `[Trait("Batch", "known-gap")]` also
  counts, and it lists each offender. Its mismatch case runs over fixture classes built for the
  test. Those classes have no `[Fact]`, so the suite scan never sees them. A second test fails if the
  suite has no `Known_gap_` tests at all, so the guard cannot pass on nothing.
- **Row 24**: `Handler<T> where T : Message` calls `message.Describe()`. `Foo` and `Bar` override it.
  `HandlerTests` has one test per instantiation. The patch `foo-describe-change` changes
  `Foo.Describe`, and both tests are selected, with `Changes` = 2.
- **Row 25**: `Temperature.ToString` is reached only through `$"Tomorrow: {t}"` in `TemperatureTests`.
  The patch `temperature-tostring-change` changes it. Result: `NothingSelected`, `Changes` = 0.
- Both patches were saved with `tests/tools/patch.cs`, and both rows passed on the first run. The
  Basics README lists the gap-shaped members.
- **Finding: `Message` has a virtual default `Describe`, not an abstract one.** Sonar S1694 fires on
  an abstract class with only abstract members. An interface would turn row 24 into row 26's gap
  (interface fan-out), so the class stays a class.
- **Gate filter (for ticket 16):**
  `dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -c Release --no-build -- --filter-namespace "Reach.Tests.Acceptance*" --filter-not-trait "Batch=known-gap"`.
  Checked with the in-process runner's
  `-list methods -namespace "Reach.Tests.Acceptance*" -trait- "Batch=known-gap"`: it lists 14 of the
  16 acceptance methods, and the two missing ones are exactly these known gaps. MTP accepts
  `--filter-not-trait`.
- **Full suite:** 645 passed, 0 failed (Windows, Release). The Release `-warnaserror` build and
  `dotnet format --verify-no-changes` are both clean.