# 13: Both CI legs green on the PR

**What to build:** The pull request carrying this work is green on both Windows and Linux, the dogfood workflow's run has been checked, and the PR is ready for the maintainer to merge. See [the spec](../spec.md).

- Open (or update) the PR from the integration branch to `main`.
- Confirm every CI step passes on both matrix legs: restore, build with warnings as errors, format verification, and test.
- Check the dogfood workflow's run. The package sets `DebugType=embedded`, so Reach's own PDBs are now embedded in its assemblies. Reach supports embedded symbols, but confirm that its self-selection still produces a sensible report, and record the result under `## Comments`.
- Do not merge. Merging is the maintainer's decision.

**Blocked by:** 10 (the summary line is culture-invariant), 11 (PDB document paths normalised at the source), 12 (long-selection tests sized from the ceiling)

**Status:** ready-for-agent

- [x] The CI workflow is green on `ubuntu-latest` and `windows-latest` for the PR's head commit
- [x] The dogfood workflow's result is checked and recorded under `## Comments`
- [x] The PR description links this spec and lists the suppressions and upstream issues recorded in ticket 09

## Comments

### Why CI failed: deterministic source paths

PR run 44512964630 failed 25 tests on both legs. Opinionated.DotNet.CodingStandards 0.0.22 sets
`ContinuousIntegrationBuild=true` when `GITHUB_ACTIONS` (or `CI`, `TF_BUILD`, ...) is set. The
SDK then records the repository root as `/_/` in every PDB and in `[CallerFilePath]`. Three test
helpers depended on the real path:

- `RepositoryRoot` walked up from `[CallerFilePath]` (20 tests, including all acceptance tests
  through `FixtureSolution`, plus `SourceRevisionRobustnessTests`). It now walks up from
  `AppContext.BaseDirectory`.
- `HelpGoldenTests` found its golden files next to `[CallerFilePath]` (2 tests). It now uses
  `RepositoryRoot`.
- `PortablePdbChecksumTests` hashed the files at the paths Reach.Core's PDB records (2 tests).
  It now maps the `/_/` prefix back to the repository root. The checksums are unchanged by the
  mapping, which is what these tests claim.

CI's determinism is left on. Checked with `GITHUB_ACTIONS=true CI=true HUSKY=0` on a fresh clone,
running exactly the ci.yml steps: Windows 528/528, and `mcr.microsoft.com/dotnet/sdk:10.0`
528/528. Restore, `-warnaserror` build and format verification were clean on both.

The acceptance tests' temporary fixture is not affected. It is copied outside the repository,
so it does not inherit the root `Directory.Build.props` or the package. `GITHUB_ACTIONS` does
reach the child `dotnet build`, but the plain SDK does not set `ContinuousIntegrationBuild`
from it, so the fixture's PDBs keep real paths.

### Dogfood workflow (run 44512964627): three separate problems

1. **`dnx: command not found` (exit 127). This was there before this branch.** `dogfood.yml`
   is unchanged from `main`. The workflow only runs on `pull_request`, so `main` has no runs.
   Every run on this PR failed the same way, including the first (44354979060), which was
   before any fixture change. The runner image does not put `dnx` on `PATH`. Even with `dotnet dnx`,
   the step would fail: `dotnet-reach` is not on nuget.org yet (the flat-container index returns
   404). The step cannot pass until 0.1.0-alpha.1 is published.
2. **"Run the whole suite" was caused by this branch.** Ticket 03 added the Contoso projects to
   `Reach.slnx`, and `dotnet test` on the solution rejects their VSTest projects under the
   testing-platform runner set in `global.json` (exit 1). ci.yml already avoids this by naming
   `tests/Reach.Tests`. Options: name the project in dogfood.yml's whole-suite step too (this
   breaks `WorkflowParityTests`' "documented recipe is the workflow" unless
   `docs/adopting-reach.md` changes with it); or take the fixture projects out of the solution
   again and build them in ci.yml as a separate step.
3. **When it does run, Reach's self-selection fails in CI. This is a product question, not
   test-infra.** I ran it locally on a CI-condition build of this branch:
   `reach select -c Release --no-build`.
   - Every first-party assembly reads as "its debug symbols point at source outside the working
     tree, so it is not first-party". The PDBs record `/_/src/...`, and Reach checks document
     paths against the working tree literally. The outcome is `failed`, which is the safe
     direction, so nothing is wrongly reported as passed. But any adopter whose CI build sets
     `ContinuousIntegrationBuild` (which Microsoft recommends for SourceLink, and which
     DotNet.ReproducibleBuilds and this package both set) gets the same failure. The PDB does
     not record the original root. SourceLink's JSON maps `/_/*` to a URL, not to a local path.
     So Reach would need a convention, for example: treat the `/_/` prefix as the git
     top-level, and `/_1/`, ... as nested source roots such as submodules. Alternatives:
     adopters build with `-p:DeterministicSourcePaths=false`, or Reach passes that property when
     it runs the build itself. Neither is in `docs/limitations.md`. Separately, this repository could set
     `DeterministicSourcePaths=false` in `Directory.Build.props`. That would make the dogfood
     meaningful, and the `/_/` mapping in `PortablePdbChecksumTests` would no longer be needed.
     This is the maintainer's decision.
   - Even with a local (non-CI) build, the run fails with "No assembly was found for
     Contoso.Core (net8.0)". The fixture's `Contoso.Core.csproj` declares
     `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>`. Inside this repository, the root
     `Directory.Build.props` sets `<TargetFramework>net10.0</TargetFramework>`, which wins, so
     only net10.0 is built. Reach reads the csproj's `TargetFrameworks` and expects both. This
     happens only now that the fixture is in the solution. The temporary copy has no
     `Directory.Build.props`, so it builds both. It is a Reach limitation (an inherited
     `TargetFramework` is not seen) and also a fixture-placement problem.
   - With net8.0 built by hand, the report looks reasonable. Base `HEAD~3` (this ticket's three
     test-only commits) selected 14 of 445 tests in Reach.Tests and skipped or ran in full the
     Contoso projects. **It looks like under-selection:** `AcceptanceTests` was not selected.
     Its constructor calls `FixtureSolution.Build`, then `RepositoryRoot.Combine`, which
     changed. `ExampleReportTests` was selected for only the one test that calls
     `RepositoryRoot` directly, not for its constructor path. Reach seems to have no edge from a
     test method to its class's instance constructor (or `Dispose`). I did not find this in
     `docs/limitations.md`.

### Final CI on PR #1 (head `b610596`, tickets 01–19 merged)

- CI run 44574563480: `build (ubuntu-latest)` and `build (windows-latest)` both pass: restore, `-warnaserror` build, format verification and test.
- Dogfood run 44574562920: Build passes. "Run the whole suite" (now `tests/Reach.Tests`, ticket 15) passes. "Select" still fails with `dnx: command not found`, which predates this branch: `dotnet-reach` is not yet published, so `dnx dotnet-reach@0.1.0-alpha.1` cannot run. A local clean-clone run of the same selection from source, under CI conditions, exits 0 with first-party assemblies recognised and correspondence `verified` (after tickets 14, 16 and 18).
- The PR description links the spec and lists the suppressions and upstream issues from ticket 09.
