# 02: The fixture copy takes only git-tracked files

**What to build:** When the acceptance harness copies the fixture solution into its temporary git repository, it copies exactly the files git tracks, never build output, IDE folders or test results that happen to be on disk. This prepares for ticket 03: once the CI build compiles the fixture in place (Release, with the coding-standards package and embedded PDBs), that output must not leak into a temporary repository. If it did, Reach running with no-build would discover extra assembly instances. See [the spec](../spec.md).

The temporary-repository build itself stays as plain as it is today.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The fixture copy enumerates git-tracked files under the fixture directory rather than every file on disk
- [ ] With the fixture built in place first (leaving `bin/` and `obj/` behind), the acceptance tests still pass, and no `bin/` or `obj/` directory appears in the temporary repository before its own build
- [ ] The existing acceptance tests pass on Windows
- [ ] No new dedicated test (agreed: the CI ordering in ticket 03 exercises this directly)
