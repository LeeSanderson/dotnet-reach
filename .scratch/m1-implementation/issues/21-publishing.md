# Publishing

Status: ready-for-human
Depends on: 19, 20
Spec: [§16.4](../../walking-skeleton/spec.md#164-documentation-ci-and-publishing)

## Goal

`dotnet-reach` on NuGet.org at `0.1.0-alpha.1`, so that the `dnx` quickstart the documentation
already contains is real rather than aspirational.

**`ready-for-human`**, because three steps cannot be delegated: creating the NuGet.org account and
package registration, configuring Trusted Publishing on nuget.org, and pushing the tag that makes
an **irreversible** publication.

## Scope

**NuGet.org, and this was settled by a finding rather than a preference.** `dnx` is a shim over
`dotnet tool exec` and resolves from the ambient `nuget.config` hierarchy with **no hardcoded
nuget.org** — proven both directions: a `<clear/>` breaks it, and a local folder feed alone
resolves it offline. **GitHub Packages is therefore not an option**: its NuGet feed requires
authentication *"regardless of whether the package is public or private"*, confirmed with live
401s, so a quickstart whose first line only works for its author is worse than no quickstart.

**`release.yml`, triggered on a `v*` tag push**: build → test → pack → push.

**Trusted Publishing via `NuGet/login@v1` with `permissions: id-token: write`.** Not a stylistic
choice: new NuGet.org API keys have been capped at 30 days, which closes the long-lived-secret
route.

**The version is hand-edited** in `Directory.Build.props`, first value `0.1.0-alpha.1`. **No MinVer
or Nerdbank.GitVersioning** — version derivation is machinery for a release cadence that does not
exist yet. The constraint that decides it: **a published package cannot be deleted, only
unlisted**, so a mistaken `0.1.0-alpha.1` is permanent, and two deliberate human acts — editing the
version and pushing the tag — are the right amount of friction for an irreversible one.

**No signing certificate.** nuget.org repository-signs automatically, and registering a certificate
makes signing **mandatory for the account from then on**.

**`ci.yml` gains its `pull_request` trigger here**, not earlier. Pre-publish, work lands on `main`
and CI gates on push; at the first tag the package exists and PRs begin. PRs exist in this
repository to set `GITHUB_BASE_REF`, not to find a reviewer who does not exist — so no branch
protection requiring approvals.

**One documentation consequence to re-check before tagging**: `dnx dotnet-reach` with a **bare
package id will not find a prerelease**, failing with a misleading "not found in NuGet feeds", and
`--prerelease` is not the fix. Ticket 20 already pins the version in the quickstart; confirm it
still does before the tag goes up, because this is the moment the docs stop describing a future
state.

**Useful and easy to forget**: `dotnet reach` dispatch depends only on `ToolCommandName`, **not on
the package id** — verified by publishing under one name and invoking under another. The package
name and the verb are independent choices.

## The human steps

1. Register the NuGet.org account and reserve `dotnet-reach` (`dotnet-` is **not** a reserved
   prefix, and all candidate ids were free).
2. Configure Trusted Publishing on nuget.org for this repository and the `release.yml` workflow,
   and set the repository variable **`NUGET_USER`** to the nuget.org account name — `NuGet/login@v1`
   takes it as an input, and it is a variable rather than a secret because an account name is not
   one.
3. Review `Directory.Build.props`'s `Version`.
4. Push the `v0.1.0-alpha.1` tag.
5. After publication, check both halves of the quickstart claim: `dnx dotnet-reach@0.1.0-alpha.1`
   resolves and runs, and the **bare** `dnx dotnet-reach` does *not* — the second is the one the
   documentation promises, and only publication can test it.

## Acceptance criteria

- `release.yml` exists, is triggered only on `v*`, and builds, tests, packs and pushes.
- A pack of the tool produces a package whose `tools/<tfm>/any/` runtimeconfig carries
  `RollForward: LatestMajor` — the property that lets a `net10.0` tool run on a machine carrying
  only .NET 11.
- The workflow declares `permissions: id-token: write` and uses `NuGet/login@v1`; no API key
  secret exists in the repository.
- **`dnx dotnet-reach@0.1.0-alpha.1` installs and runs `dotnet reach --help` on a clean machine**,
  and the bare form is confirmed *not* to resolve the prerelease — both checked after publication,
  because that is the only place the quickstart is real.
- `ci.yml` has its `pull_request` trigger.
- No `.snupkg`, no SourceLink, no Dependabot, no coverage gate — each a deliberate omission with a
  reason; do not add them here.

## Out of scope

`dogfood.yml` and the docs↔workflow parity test — ticket 22, which **cannot be written before this
one lands**, because its recipe is a `pull_request` workflow installing a *published* package.

## Implementation notes

**Everything delegable is done; the ticket stays `ready-for-human` for the four steps above.**
`release.yml` is written and `ci.yml` has its `pull_request` trigger. Nothing here publishes
anything: the workflow fires only on a `v*` tag, and no tag has been pushed.

**The workflow carries one guard the ticket did not ask for, and it is the one worth having.**
Before it restores anything it reads `Version` out of `Directory.Build.props` and fails if the tag
does not match. The version and the tag are edited in different files by different acts, and the
whole reason this ticket is two deliberate human acts is that a published package cannot be
deleted — a guard that costs four lines and turns *permanent* into *try again* belongs on that
step.

**Package metadata was added, because the pack was not publishable without it.** Ticket 20 asserts
that a NuGet page lands a reader on GitHub, and the package had no `PackageProjectUrl`, no
description and no licence expression, so it did not. `Description`, `Authors`,
`PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl` and `PackageTags` are now set.

**Deliberately no `PackageReadmeFile`,** and the `NU5039`-adjacent pack warning about a missing
readme is accepted rather than silenced. nuget.org does not resolve relative links, and this README
is almost entirely links into `docs/`; embedding it would render a page of dead ones. The
`projectUrl` is how a reader reaches the documents, in the place the links work.

**Verified locally**, since these are the criteria that do not need a publication:

- `dotnet pack` produces `tools/net10.0/any/` whose `Reach.Cli.runtimeconfig.json` carries
  `"rollForward": "LatestMajor"`.
- The nuspec is well-formed, carries `<packageType name="DotnetTool" />`, and records the commit.
- Both workflow files parse; `release.yml` triggers on `push.tags: ['v*']` and nothing else; the
  version-guard shell was run against the real `Directory.Build.props`.
- No API key secret exists anywhere in the repository — `NuGet/login@v1` with `id-token: write` is
  the only credential path.
