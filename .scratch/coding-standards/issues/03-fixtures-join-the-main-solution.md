# 03: The fixtures join the main solution

**What to build:** The CI build compiles the fixture solution as part of the main solution, which proves the fixture builds on its own. `dotnet test` still runs only Reach's own tests. See [the spec](../spec.md).

- Add the fixture solution's five projects to the main solution under a `/samples/` solution folder, at their current location.
- Point the CI `Test` step at Reach's own test project explicitly instead of the solution. The fixture's NUnit and framework-less test projects use VSTest, and the repository's testing-platform runner setting in `global.json` would reject them. The fixture's tests keep running only through Reach, inside the acceptance tests.
- Add the rule to the acceptance-layer spec: every sample solution is in the main solution under `/samples/` and builds clean under the coding standard. Add it to that spec's decisions, wherever sample solutions are declared, so samples not yet written are designed to meet it.

**Blocked by:** 02 (the fixture copy takes only git-tracked files)

**Status:** ready-for-agent

- [x] The main solution lists the fixture's five projects under `/samples/`
- [x] `dotnet build` of the main solution with warnings as errors succeeds and compiles the fixture projects
- [x] The CI `Test` step runs Reach's test project only, and no fixture test project is executed by `dotnet test`
- [x] The acceptance tests pass after the in-place build, with no leaked build output (relies on 02)
- [x] The acceptance-layer spec states the sample-membership rule

## Comments

- Verified locally on Windows: `dotnet build -c Release -warnaserror` compiles all five fixture
  projects with 0 warnings; `dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -c Release
  --no-build` runs only `Reach.Tests.dll`, 521 passed, with the fixture's `bin/`/`obj/` present.
  `dotnet format --verify-no-changes` reports nothing in the fixture; its remaining WHITESPACE
  errors are the known CRLF ones in Reach's own code (another ticket).
- `release.yml`'s `Test` step had the same solution-wide `dotnet test`, so it now targets
  `Reach.Tests` too; otherwise the release build would hit the same VSTest rejection.
- The acceptance-layer spec is not written yet (its ticket 13), so the rule went into the map's
  decisions, which the spec is written from, and as an amendment on ticket 04, which declares
  where samples live. That amendment also corrects 04's "a sample is still never built in place".
- **Open, for the maintainer:** `dogfood.yml` still runs `dotnet test -c Release --no-build` on
  the whole solution ("Run the whole suite"), which will now hit the fixture's VSTest projects,
  and Reach's own selection there will see the fixture's test projects in the solution. Left
  unchanged because that workflow is meant to be a recipe an adopter copies unchanged; it needs a
  decision (target `Reach.Tests` there too, or keep the recipe generic).
