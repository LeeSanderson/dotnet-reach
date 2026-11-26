# Publishing

Status: ready-for-human

The first release of Reach: `dotnet-reach` `0.1.0-alpha.1` on NuGet.org. Everything here except
the publication itself is built. What remains is in [ticket 01](issues/01-publish-0.1.0-alpha.1.md),
and only a human can do it.

This spec was put together on 2026-11-26 from the finished efforts it depended on, which were then
removed from `.scratch/`. Their full text stays in git history:

- [the walking-skeleton spec](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/walking-skeleton/spec.md), §16.4 for the
  publishing decisions in §2 here;
- [the acceptance-layer spec](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-layer/spec.md), §7 and §9 for the
  release gate, now in [`docs/release-gate.md`](../../docs/release-gate.md);
- [the M1 implementation tickets](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/m1-implementation/README.md) and
  [the acceptance implementation tickets](https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-implementation/README.md). A bare
  "implementation ticket NN" below is one of the latter.

## 1. Goal

`dotnet-reach` on NuGet.org at `0.1.0-alpha.1`, so that the `dnx` quickstart the documentation
already contains is real rather than aspirational. The release waits for the gate (§3): every
claim in the docs proven by a passing test. The gate audit (implementation ticket 16) filled its claim-to-suite tables,
and its two follow-ups (implementation tickets 21 and 22) have landed, so the gate is met.

## 2. Decisions

From walking-skeleton spec §16.4 and M1 ticket 21.

- **NuGet.org.** `dnx` resolves from the ambient `nuget.config` hierarchy, and GitHub Packages'
  NuGet feed requires authentication whether or not the package is public, so a `dnx` quickstart
  on it would work only for its author.
- **`release.yml`, on a `v*` tag push**: build, test, pack, push. It is written, and it fails if
  the tag does not match `Version` in `Directory.Build.props`.
- **Trusted Publishing** via `NuGet/login@v1` with `permissions: id-token: write`, because new
  NuGet.org API keys are capped at 30 days. No API key secret exists in the repository.
- **The version is hand-edited** in `Directory.Build.props`, first value `0.1.0-alpha.1`. No MinVer
  or Nerdbank.GitVersioning. A published package **cannot be deleted, only unlisted**, so two
  deliberate human acts (editing the version, pushing the tag) are the right friction for an
  irreversible one.
- **No signing certificate.** nuget.org repository-signs automatically, and registering a
  certificate makes signing mandatory for the account from then on.
- **Install guidance leads with `dnx dotnet-reach@<version>`, never `-g`.** The pin is a
  requirement: `dnx` with a bare package id will not find a prerelease, and fails with a misleading
  "not found in NuGet feeds". `Reporting/WorkflowParityTests` asserts every documented `dnx` line
  pins the version this repository builds.
- **`dogfood.yml` runs Reach on Reach and does not gate.** The selection runs alongside the whole
  suite, and only the whole suite can fail the job. Gating on Reach's own selection is the unproven
  narrowing PRD §8 forbids. Until the package exists, its selection step fails at `dnx`, and the
  workflow lets that through.
- **No `PackageReadmeFile`.** nuget.org does not resolve relative links and the README is mostly
  links into `docs/`; `PackageProjectUrl` takes a reader to them instead.
- **Deliberately not done**: no `.snupkg` or SourceLink, no Dependabot, no coverage gate, no branch
  protection requiring approvals. Do not add them as part of the release.
- **`dotnet reach` dispatch depends only on `ToolCommandName`**, not on the package id, so the
  package name and the verb are independent choices.

## 3. The release gate

Its rules, rows 1 to 29 and the claim-to-suite tables are in
[`docs/release-gate.md`](../../docs/release-gate.md).

## 4. Tickets

| # | Ticket | Status |
|---|---|---|
| 01 | [Publish 0.1.0-alpha.1](issues/01-publish-0.1.0-alpha.1.md) | ready-for-human |
