# 15: The dogfood workflow runs Reach's own tests

**What to build:** The dogfood workflow's "Run the whole suite" step passes again. Ticket 03 put the Contoso fixture's VSTest projects into `Reach.slnx`, and `dotnet test` on the solution rejects them under the testing-platform runner that `global.json` sets. See [the spec](../spec.md) and ticket 13's `## Comments`.

Maintainer decision: "Point dogfood at the right tests".

- `.github/workflows/dogfood.yml` names `tests/Reach.Tests/Reach.Tests.csproj`, as `ci.yml` does.
- The selection step is scoped the same way, so Reach never emits an invocation for a fixture test project that the run step would then reject.
- `docs/adopting-reach.md` carries the same YAML, because `WorkflowParityTests` compares them byte for byte. The recipe stays useful to adopters: it says where to put their own solution or test project.
- The `dnx: command not found` failure is not in scope. It is there because `dotnet-reach` is not yet published.

**Blocked by:** None

**Status:** ready-for-agent

- [ ] "Run the whole suite" in `dogfood.yml` names Reach's test project
- [ ] The select step is scoped to the same test project
- [ ] `docs/adopting-reach.md` tells adopters what to put in place of Reach's test project
- [ ] `WorkflowParityTests` pass

## Comments
