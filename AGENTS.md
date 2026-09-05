# dotnet-reach

Reach is a CLI tool that works out which tests a code change could possibly affect,
by reading the compiled assemblies and walking the call graph backwards from changed
code to tests. See [PRD.md](PRD.md) for the full product definition.

## Agent skills

### Issue tracker

Issues and specs live as markdown files under `.scratch/<feature-slug>/` in this repo. See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical triage roles, used verbatim as `Status:` values. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
