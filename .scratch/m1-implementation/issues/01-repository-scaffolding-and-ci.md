# Repository scaffolding and the CI gate

Status: ready-for-agent
Depends on: (none)
Spec: [§16.1](../../walking-skeleton/spec.md#161-projects-ports-and-seams), [§16.4](../../walking-skeleton/spec.md#164-documentation-ci-and-publishing)

## Goal

A solution that builds, tests and formats cleanly on both operating systems, from the first
commit. Nothing else in this effort can land until it does.

## Scope

**Projects**, exactly these three, no more:

```
Reach.Cli      → System.CommandLine 2.0.x; references Reach.Core
Reach.Core     → Microsoft.CodeAnalysis.CSharp 5.9.0, System.Reflection.Metadata
Reach.Tests    → xunit.v3 4.0.0; InternalsVisibleTo from Core
```

No `Reach.Contracts`. Nothing in `Reach.Core` is `public` — the CLI is the only contract.

**Solution file**: `.slnx`, which is the default for `dotnet new sln` on .NET 10.

**`Directory.Build.props`**: `TargetFramework` `net10.0`, `Nullable` enable, `LangVersion`
default, `TreatWarningsAsErrors`, and the hand-edited `Version` with first value
`0.1.0-alpha.1`. `Reach.Cli` additionally sets `PackAsTool`, `ToolCommandName` `reach`,
`PackageId` `dotnet-reach`, and **`RollForward` `LatestMajor`** — which is easy to lose: the
default policy is `Minor` and will not cross a major version, so without it a `net10.0` tool
fails on a machine carrying only .NET 11.

**`global.json`** — mandatory, not a preference. Two blocks:

- `"sdk": { "version": "<the band the developer machine carries>", "rollForward": "latestFeature" }`.
  The explicit policy is the load-bearing half: an omitted `rollForward` defaults to `patch`,
  the narrowest there is, and every policy ends in "else fail".
- `"test": { "runner": "Microsoft.Testing.Platform" }`. Without it, `dotnet test` against an
  `xunit.v3` 4.0.0 project on the .NET 10 SDK is a **hard build error** — the block comes from
  MTP v2's own MSBuild targets, and adding `xunit.runner.visualstudio` does not rescue it.

`Reach.Tests` needs `<OutputType>Exe</OutputType>`, which MTP enforces with a bespoke MSBuild
error.

**`.github/workflows/ci.yml`** — push to `main`. Matrix `ubuntu-latest` × `windows-latest`.
Steps: checkout → restore → `dotnet build -warnaserror` →
`dotnet format --verify-no-changes` →
`dotnet test -c Release --no-build --report-xunit-trx --results-directory ./TestResults`.

- **No `actions/setup-dotnet`.** Both runner images already carry several .NET 10 feature
  bands; `global.json` pins the floor.
- **TRX needs no extra package** via `--report-xunit-trx`. `--report-trx` and `--coverage` are
  Microsoft extensions that exit 5 without their packages — do not use them.
- **Configure `git` identity** in the workflow (`user.email`, `user.name`). They are unset on
  hosted runners and `git commit` fails without them, which later tickets' temp-repository
  fixtures depend on.
- **No path filters**, no coverage gate, no Dependabot, no branch protection requiring
  approvals. Each is a deliberate omission with a reason in spec §16.4; do not add them.

**Both operating systems, and this is the one place worth the minutes.** Reach identifies a
first-party assembly by the source documents its debug symbols point at, and routes a changed
file by matching those paths against the working tree. Separators and case sensitivity are
exactly where Windows and Linux differ, and the tool is developed on Windows, so Linux is the
untested half. A path-comparison bug there is silent under-selection.

**A pre-commit hook** running `dotnet format` on staged files. The hook keeps formatting out
of the way; CI is what makes it true.

## Acceptance criteria

- `dotnet build` and `dotnet test` succeed on a clean checkout on Windows and Linux.
- `dotnet format --verify-no-changes` passes.
- `ci.yml` is green on both matrix legs.
- A deliberately unformatted commit fails the gate.
- `Reach.Tests` can reach `Reach.Core` internals; nothing in `Reach.Core` is `public`.

## Out of scope

`release.yml` and `dogfood.yml` — tickets 21 and 22. `ci.yml` gains its `pull_request`
trigger at first publish, not here.
