# 13: Both CI legs green on the PR

**What to build:** The pull request carrying this work is green on both Windows and Linux, the dogfood workflow's run has been checked, and the PR is ready for the maintainer to merge. See [the spec](../spec.md).

- Open (or update) the PR from the integration branch to `main`.
- Confirm every CI step passes on both matrix legs: restore, build with warnings as errors, format verification, and test.
- Check the dogfood workflow's run. The package sets `DebugType=embedded`, so Reach's own PDBs are now embedded in its assemblies. Reach supports embedded symbols, but confirm that its self-selection still produces a sensible report, and record the result under `## Comments`.
- Do not merge. Merging is the maintainer's decision.

**Blocked by:** 10 (the summary line is culture-invariant), 11 (PDB document paths normalised at the source), 12 (long-selection tests sized from the ceiling)

**Status:** ready-for-agent

- [ ] The CI workflow is green on `ubuntu-latest` and `windows-latest` for the PR's head commit
- [ ] The dogfood workflow's result is checked and recorded under `## Comments`
- [ ] The PR description links this spec and lists the suppressions and upstream issues recorded in ticket 09
