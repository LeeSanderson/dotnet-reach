# The harness, and Basics' first row

Status: ready-for-agent
Depends on: 01
Spec: [§2.1](../../acceptance-layer/spec.md#21-where-they-live-and-the-main-solution), [§2.3](../../acceptance-layer/spec.md#23-basics), [§3.5](../../acceptance-layer/spec.md#35-drift), [§4](../../acceptance-layer/spec.md#4-declaring-an-acceptance-test), [§5](../../acceptance-layer/spec.md#5-the-harness)

## Goal

The whole acceptance-test surface (`Baseline`, `Apply`, `Expect`, the `Expected` record and every
check `Expect` makes), proven end to end by row 1 on a first cut of the Basics sample.

## Scope

**The Basics sample, first cut** at `tests/samples/Basics/` (spec §2.3): `Basics.slnx`,
`README.md`, `global.json` with the testing-platform runner setting and nothing else that affects
the build, `Basics.Core` (`net10.0`) and `Basics.Tests` (`xunit.v3` 4.0.0). Only what row 1 needs.
The generator project, `Basics.Tools` and the other rows' types come with their tickets. Add the
projects to `Reach.slnx` under `/samples/`. They build clean under the coding standard.

**`SampleCopy`** (spec §5.5): rework `tests/Reach.Tests/Fixtures/FixtureSolution.cs`, do not write
a second helper. It takes a sample name and implements spec §5.1's seven steps and §5.2's build-mode
rule, with `repo/` and `out/` side by side under one temp root that is deleted on dispose. Keep the
old API (`Build`, `WriteFile`, `Touch`, `Rebuild`) working for `Acceptance/AcceptanceTests.cs`,
which still runs over `tests/fixtures/Contoso/` until ticket 12. Delete the stale class-fixture
comment.

**The builder**: `Baseline(...)` (at most one), `Apply(...)` (one or more), `Expect(Expected)`, on
a base class or helper that each sample's test class names its sample to once (spec §4.1). Patches
are read from `tests/Reach.Tests/Acceptance/<Sample>/` in the source tree. A failed apply reports
as spec §3.5 says, including the fix line (the tool it names arrives in ticket 03; the line is
right regardless).

**`Expected` and `Expect`** exactly as spec §4.3 and §4.4:

- the four required members, `ExitCode` (required exactly when `Outcome` is `Failed`, checked at
  run time), and the six optional sections, each exact when given;
- `Name@tfm` keys and the ambiguity error for a bare name matching several entries;
- one failure listing **every** differing section, `Because` first, with no pasteable literal;
- the four invariants: determinism (scrubber per §5.4, string comparison, diff on mismatch, no
  Verify), exit code matches outcome, skip means zero invocations, run in full means
  `"total": "unknown"`.

Test the comparison and the invariants **in memory** against hand-built `Report` values
(`Fixtures/ReportFixture.cs` is prior art). Each invariant and each section needs a test that
makes it fail and checks the message, because a comparison that never fails proves nothing.

**Row 1** (spec §7.1) in `tests/Reach.Tests/Acceptance/Basics/BasicsTests.cs`: a body change that
selects exactly the tests reaching it through compiled edges. Write its patch with `git diff`, then
strip the `diff --git` and `index` lines by hand (the tool arrives in ticket 03). Then delete
`A_change_selects_exactly_the_tests_that_reach_it` **and**
`The_same_input_twice_produces_byte_identical_json` from `AcceptanceTests.cs`: row 1 and the
determinism invariant replace them (spec §8.1).

## Acceptance criteria

- `tests/samples/Basics/` exists, is in `Reach.slnx` under `/samples/`, and builds clean with
  `-warnaserror`.
- Row 1 passes on Windows and Linux under `ParallelMode.All`.
- Every `Expected` section and every invariant has an in-memory test that makes it fail, and the
  failure message names the section, shows `Because` first, and lists all differences at once.
- A missing patch and a patch that does not apply each fail with the spec §3.5 message (one test
  each, over a deliberately broken patch in a temp folder, not a checked-in broken patch).
- The `git status --porcelain` check runs before the first Reach run, and a stray file in `repo/`
  fails it.
- The two old tests are gone. The remaining old tests still pass over Contoso.

## Out of scope

The patch tool and the stripped-form guard (ticket 03). The known-gap trait (ticket 09). Moving or
renaming Contoso (ticket 12). Persistent slots (spec §5.6).
