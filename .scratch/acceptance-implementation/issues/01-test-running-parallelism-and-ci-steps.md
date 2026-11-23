# Test running: full parallelism and the two CI steps

Status: resolved
Depends on: —
Spec: [§5.3](../../acceptance-layer/spec.md#53-parallelism), [§6](../../acceptance-layer/spec.md#6-where-the-acceptance-tests-run)

## Goal

Set up how the suite runs before the layer grows. Acceptance tests run in parallel inside one
class, and CI reports the fast layer and the acceptance layer as two separate steps.

## Scope

**Full parallelism.** Set `ParallelMode.All` for `Reach.Tests` (`xunit.v3` 4.0.0), with
`maxParallelThreads` left at its default.

1. Run the whole suite under `All`, on Windows and on Linux (WSL or a container is fine).
2. Mark every test that breaks with `DisableParallelization`, on the method or the class. Fix the
   cause instead where the cause is a shared temp path or a static the test owns.
3. **If more than a handful of tests break, stop and fall back to `Collections`.** Record which
   ones broke and why, and the serial floor this costs, in this ticket's comments.

**CI.** In `.github/workflows/ci.yml`, replace the single test step with two steps in the same
matrix job, sharing its one build:

1. `dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -c Release --no-build ... -- --filter-not-namespace "Reach.Tests.Acceptance*"`
2. `dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -c Release --no-build ... -- --filter-namespace "Reach.Tests.Acceptance*"`

Keep the existing results reporting and upload for both steps (distinct result file names). Give
the acceptance step `timeout-minutes: 20`, with a comment saying it guards against a hung build and
is **not** a time budget.

**Contributor docs.** Add a short `CONTRIBUTING.md` (none exists) saying:

- plain `dotnet test` runs everything, acceptance tests included;
- to narrow a run, use an ordinary filter for the area you touched, for example
  `dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -- --filter-namespace "Reach.Tests.Graph*"`;
- running Reach on its own repository is not a fast path, because almost any change selects the
  whole acceptance layer, which is correct.

Link it from the README's "Where to go next".

## Acceptance criteria

- `Reach.Tests` runs under `ParallelMode.All`, or under `Collections` with the reason recorded.
- The whole suite is green on both OSes, run several times in a row (flakiness from parallelism
  shows up on repeat runs).
- CI has two test steps, named so a failure says which layer failed. The acceptance step has
  `timeout-minutes: 20` and its comment.
- `CONTRIBUTING.md` exists and the README links it.

## Out of scope

Any change to the acceptance tests themselves. A time budget of any kind (spec §6).

## Comments

**Done.** `tests/Reach.Tests/Parallelization.cs` sets
`[assembly: Xunit.v3.Parallelization(Mode = ParallelMode.All)]` (xunit.v3 4.0.0's replacement for
the deprecated `CollectionBehavior` properties), with `MaxThreads` left at its default. **No test
broke under `All`**, so nothing carries `DisableParallelization` and there was no fallback to
`Collections`. A scan found no shared temp path or test-owned static: `TempDirectory` names each
directory with a GUID, and `SpanJoinTests`' fixed `reach-deterministic-join` path is only ever read,
never written.

CI: `Test (fast layer)` and `Test (acceptance layer)` over the one build, writing `fast.trx` and
`acceptance.trx` into the uploaded `./TestResults`. The acceptance step has `timeout-minutes: 20`
and the "hung build, not a budget" comment. It runs whenever the fast step ran, pass or fail, so a
red fast layer still reports the acceptance layer. `--list-tests` confirms the split: 553 fast +
12 acceptance = 565, with no acceptance test in the fast list. `CONTRIBUTING.md` is added and
linked from the README.

**Runs (Windows, 565 tests):** baseline under `Collections` was 10m16s. Under `All`: 6m35s, then
2m47s and 3m00s, all green. (The first run under `All` was cold, and runs 2 and 3 overlapped a
container build.)

**Finding: the Linux run is not done.** WSL has no .NET SDK, so the Linux pass ran in a
`mcr.microsoft.com/dotnet/sdk:10.0` container. It built clean, but the host stopped it for low
memory during its first test run, before any results came in. The Linux side of this ticket's
acceptance criterion is still open: CI's `ubuntu-latest` leg is the first real check, or rerun the
container on a machine with more memory.
