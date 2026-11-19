# 18: Tracked sources are not read as untracked and git-ignored

**What to build:** A run whose target is a project in a subdirectory (the dogfood run on `tests/Reach.Tests/Reach.Tests.csproj`) sees every tracked and untracked file in the repository. Today it reports `ignored-untracked-assembly` for all 166 tracked sources in Reach.Cli, Reach.Core and Reach.Tests, and it silently misses an untracked file outside the target's directory.

Found while checking the dogfood command from ticket 15 (Windows, in the integration worktree and in a fresh clone, with or without CI env vars).

**Blocked by:** None

**Status:** ready-for-agent

- [x] Tracked and untracked files anywhere in the repository stay visible when git runs from the target project's directory (test-first, at `SourceVisibility.ReadAsync`)
- [x] An untracked file outside a test-project target is in the changed set (test-first, at `ChangedSetBuilder` through `ChangeRepository`)
- [x] The dogfood command (`select tests/Reach.Tests/Reach.Tests.csproj -c Release --no-build --base origin/main`) no longer prints `ignored-untracked-assembly` for tracked files
- [x] Full suite, `-warnaserror` build and `dotnet format --verify-no-changes` clean

## Comments

**Root cause.** `ReachPipeline` builds its `GitAdapter` with the target's directory as the working directory. When the target is a project in a subdirectory, `git ls-files --cached` and `git ls-files --others --exclude-standard` list only that subtree, and spell each path relative to the subdirectory. `SourceVisibility` combines those paths with the repository root. The result: every file outside the target's directory is missing from the visible set, and every file inside it is spelled wrongly (`<root>/Acceptance/...` instead of `<root>/tests/Reach.Tests/Acceptance/...`). So every tracked first-party document reads as untracked and ignored. `git diff --name-status` is not affected, because it always prints root-relative paths.

The same command feeds `ChangedSetBuilder`. There the bug is worse than a false notice: a new, untracked file outside the target's directory (for example `src/Reach.Core/New.cs` on a run against the test project) is not in the changed set at all, so its tests are silently not selected. One inside the target's directory gets a path without the directory prefix, so it maps to no project.

**Not a regression.** `main` (752d92e) builds the adapter in `target.Directory` in the same way, and the same dogfood command on a `main` worktree prints the same notice (143 files). It was not seen before because the dogfood run targeted the solution at the repository root until ticket 15 pointed it at the test project. None of the suspected changes (ticket 11's `PdbDocuments.PathOf`, ticket 14's `DeterministicSourcePaths.Map`, ticket 07's refactors, ticket 16's TFM change, separators, case or CRLF) is involved: the PDB paths were correct; the git listing was not.

**Fix.** `GitAdapter` appends `--full-name -- :/` to both `ls-files` calls. `:/` is the pathspec for the whole repository from any subdirectory, and `--full-name` spells the paths from the root, the same spelling `diff` returns. The git working directory stays the target's directory. No ADR: this is a bug fix with no design choice, and it is recorded at `GitAdapter.WholeRepository`. No limitation added or removed.

**Checked.** Windows, fresh clone: `dotnet build -c Release -warnaserror` clean, `dotnet format --verify-no-changes` clean, full suite 563/563, and the dogfood command exits 0 with no `ignored-untracked-assembly` notice. Linux, fresh clone in `mcr.microsoft.com/dotnet/sdk:10.0` with `GITHUB_ACTIONS=true CI=true HUSKY=0`: the commit before the fix prints the notice for 166 files, the same as Windows, so the bug is not platform-specific. With the fix: the build is clean, the suite passes 563/563, and the dogfood command exits 0 without the notice.
