# 02: The fixture copy takes only git-tracked files

**What to build:** When the acceptance harness copies the fixture solution into its temporary git repository, it copies exactly the files git tracks, never build output, IDE folders or test results that happen to be on disk. This prepares for ticket 03: once the CI build compiles the fixture in place (Release, with the coding-standards package and embedded PDBs), that output must not leak into a temporary repository. If it did, Reach running with no-build would discover extra assembly instances. See [the spec](../spec.md).

The temporary-repository build itself stays as plain as it is today.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] The fixture copy enumerates git-tracked files under the fixture directory rather than every file on disk
- [x] With the fixture built in place first (leaving `bin/` and `obj/` behind), the acceptance tests still pass, and no `bin/` or `obj/` directory appears in the temporary repository before its own build
- [x] The existing acceptance tests pass on Windows
- [x] No new dedicated test (agreed: the CI ordering in ticket 03 exercises this directly)

## Comments

- `FixtureSolution.Copy` now copies what `git ls-files -z` lists under the fixture directory, so build output, IDE folders and test results are never copied.
- `SourceDirectory` searched up for a `.git` *directory*, which a linked worktree does not have (there `.git` is a file). From a worktree it used to find the main checkout and copy *that* fixture. It now accepts either a file or a directory, so the copy comes from the checkout under test.
- Verified on Windows: built `tests/fixtures/Contoso/Contoso.slnx` in place, so `bin/` and `obj/` were on disk. Then ran the acceptance tests (12 passed) with a temporary assertion that no `bin/` or `obj/` directory existed in the temporary repository after the copy. Removed the assertion, as agreed (no new dedicated test). Full suite: 521 passed.