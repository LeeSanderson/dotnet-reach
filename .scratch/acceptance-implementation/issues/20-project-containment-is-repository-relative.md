# Project containment is relative to the repository

Status: resolved
Depends on: 17, 18, 19

## Goal

When Reach looks up the project a changed file belongs to, it compares the file's path with the
project directories using the same base. That closes an under-selection when the solution, or all
of its in-scope projects, sit below a subdirectory of the repository.

## Background

Found by ticket 19 (see its `## Comments`), and the owner decided to fix it in this PR on
2026-11-23. `RootSets.ProjectFor` resolves paths against the common root of the in-scope projects,
but changed paths are relative to the repository. When every in-scope project sits below one
directory (all under `src/`, or a solution under `app/`), two things go wrong. An unjoined in-scope
member matches no project and roots nothing. Recompilation widening finds no declaring project, so
a changed constant can select nothing. With a single in-scope project the opposite happens: every
path matches it. Ticket 19 added `AnalysisScope.ProjectContaining`, which does the containment
correctly, and `TierLadder` and `ChangedSetBuilder` already share it.

## Scope

1. `RootSets.ProjectFor`, and anything else that matches changed paths against project directories,
   uses `AnalysisScope.ProjectContaining`, or an equivalent with the same base. Look for any other
   place that matches paths against the in-scope projects' common root, and fix it the same way.
2. Add tests at the seam with the in-scope projects below a subdirectory of the repository.
   - An unjoined member widens its own assembly.
   - A changed constant widens its transitive referencers.
   - A single in-scope project no longer claims a file outside its directory.
   - Each test should fail before the fix.
3. Check that the existing acceptance rows are unchanged. The samples sit at the root of their
   temporary repositories, so they shouldn't move. If one does move, work out why by hand before
   changing its expected result.

## Acceptance criteria

- The new seam tests pass, and each was seen to fail first.
- No acceptance row's expected result changes, or each change is explained in the comments.
- The full suite is green, and the Release `-warnaserror` build and format check are clean.

## Comments

- **Done.** `RootSets.ProjectFor` and its common-root helpers are deleted. The unjoined-member
  fallback and recompilation widening's declaring-project lookup now call
  `TierLadder.ProjectContaining`, which resolves against the repository root and delegates to
  `AnalysisScope.ProjectContaining`. A search of `src/` found no other place that matched paths
  against the projects' common root (`ReportBuilder`'s `GetRelativePath` only formats paths for
  display).
- Seam tests in `Selection/ProjectContainmentTests` put every in-scope project below `/repo/app/`.
  An unjoined member widens its own assembly. A changed constant widens Api, its referencer. A
  single in-scope project no longer claims `tools/Script/Script.cs`: that change has no roots and
  is marked outside the analysis scope. All three failed first, each on the assertion it targets.
- The third test is the one narrowing, and it is the one the ticket describes: a file that no
  in-scope project contains can't be compiled into any test process.
- No acceptance row's expected result changed.
