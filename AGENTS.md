# dotnet-reach

Reach is a CLI tool that works out which tests a code change could possibly affect,
by reading the compiled assemblies and walking the call graph backwards from changed
code to tests. See [PRD.md](PRD.md) for the full product definition.

## Building and testing

Several agents often build at once, each in its own worktree, on one machine. A top-level build
starts one MSBuild node per processor and keeps them alive afterwards, so concurrent builds
exhaust memory. Build with a capped node count, then test without building:

```sh
dotnet build -c Release -warnaserror -m:4
dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -c Release --no-build
dotnet format --verify-no-changes --no-restore
```

The test command targets `Reach.Tests` only; sample tests run only through Reach. Narrow it
with `-- --filter-namespace "Reach.Tests.<Area>*"` while iterating. The sample builds inside
acceptance tests are capped separately, by `tests/Reach.Tests/Fixtures/BuildSlots.cs`.

## Agent skills

### Issue tracker

Issues and specs live as markdown files under `.scratch/<feature-slug>/` in this repo. See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical triage roles, used verbatim as `Status:` values. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
