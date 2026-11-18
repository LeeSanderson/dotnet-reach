# 03: The fixtures join the main solution

**What to build:** The CI build compiles the fixture solution as part of the main solution, which proves the fixture builds on its own. `dotnet test` still runs only Reach's own tests. See [the spec](../spec.md).

- Add the fixture solution's five projects to the main solution under a `/samples/` solution folder, at their current location.
- Point the CI `Test` step at Reach's own test project explicitly instead of the solution. The fixture's NUnit and framework-less test projects use VSTest, and the repository's testing-platform runner setting in `global.json` would reject them. The fixture's tests keep running only through Reach, inside the acceptance tests.
- Add the rule to the acceptance-layer spec: every sample solution is in the main solution under `/samples/` and builds clean under the coding standard. Add it to that spec's decisions, wherever sample solutions are declared, so samples not yet written are designed to meet it.

**Blocked by:** 02 (the fixture copy takes only git-tracked files)

**Status:** ready-for-agent

- [ ] The main solution lists the fixture's five projects under `/samples/`
- [ ] `dotnet build` of the main solution with warnings as errors succeeds and compiles the fixture projects
- [ ] The CI `Test` step runs Reach's test project only, and no fixture test project is executed by `dotnet test`
- [ ] The acceptance tests pass after the in-place build, with no leaked build output (relies on 02)
- [ ] The acceptance-layer spec states the sample-membership rule
