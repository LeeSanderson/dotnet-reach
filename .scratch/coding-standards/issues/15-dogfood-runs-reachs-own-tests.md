# 15: The dogfood workflow runs Reach's own tests

**What to build:** The dogfood workflow's "Run the whole suite" step passes again. Ticket 03 put the Contoso fixture's VSTest projects into `Reach.slnx`, and `dotnet test` on the solution rejects them under the testing-platform runner that `global.json` sets. See [the spec](../spec.md) and ticket 13's `## Comments`.

Maintainer decision: "Point dogfood at the right tests".

- `.github/workflows/dogfood.yml` names `tests/Reach.Tests/Reach.Tests.csproj`, as `ci.yml` does.
- The selection step is scoped the same way, so Reach never emits an invocation for a fixture test project that the run step would then reject.
- `docs/adopting-reach.md` carries the same YAML, because `WorkflowParityTests` compares them byte for byte. The recipe stays useful to adopters: it says where to put their own solution or test project.
- The `dnx: command not found` failure is not in scope. It is there because `dotnet-reach` is not yet published.

**Blocked by:** None

**Status:** ready-for-agent

- [x] "Run the whole suite" in `dogfood.yml` names Reach's test project
- [x] The select step is scoped to the same test project
- [x] `docs/adopting-reach.md` tells adopters what to put in place of Reach's test project
- [x] `WorkflowParityTests` pass

## Comments

- Both steps now name `tests/Reach.Tests/Reach.Tests.csproj`: `select <path>` and `dotnet test --project <path>`. The Build step still builds the whole solution. PRD §8.2 says a single-test-project run must still have the whole solution's output.
- Why the select step is scoped too: over `Reach.slnx`, Reach treats the Contoso test projects as test projects in scope. A change to fixture code would then make it emit `dotnet test` invocations for those VSTest projects, which the run step would reject for the same reason as the whole suite. Pointing Reach at one test project is legitimate under ADR-0002, because scope is the union of the test-project closures.
- `WorkflowParityTests` compares the fenced block byte for byte, so the block cannot hold a placeholder. Instead, a YAML comment above the select step (in both files) and one sentence before the block tell adopters to put their own solution or test project in place of the path, or to drop it (with `--project`) so that both steps use the solution in the working directory.
- Checked locally: the 5 `WorkflowParityTests` pass. `dotnet run --project src/Reach.Cli -- select tests/Reach.Tests/Reach.Tests.csproj --base coding-standards` exits 0, and the report's only entry is `tests/Reach.Tests/Reach.Tests.csproj`. Its `ignored-untracked-assembly` notice is the `/_/` source-path problem from ticket 14, not this ticket.
- Not fixed, already there before this ticket: the select step still fails with `dnx: command not found` (exit 127). `dnx` is not on the runner's `PATH`, and `dotnet-reach` 0.1.0-alpha.1 is not yet on nuget.org (see ticket 13). The whole-suite step has `if: !cancelled()`, so it still runs and gates.
