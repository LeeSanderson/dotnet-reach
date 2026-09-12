# CI and publishing

Research for [issue 21](../issues/21-ci-for-the-reach-repository.md). Investigated 11 September 2026.

Machine used for the empirical checks: Windows 11, with SDKs 6.0.428, 8.0.425, 9.0.308,
9.0.318, 10.0.302 and **10.0.303** installed (`dotnet --list-sdks`). Every experiment ran in
a scratch directory against packages built or downloaded there. **Nothing in the repository
was built or modified**, and nothing was installed into the user's global tool directory —
the tool-install experiments all used `--tool-path` into scratch.

---

## Recommendations

1. **Publish to NuGet.org, not GitHub Packages.** GitHub's NuGet registry requires a token
   for *every* read, including public packages — anonymous restore is impossible, confirmed
   by docs and by a live `401` (§1.3). That single fact kills the `dnx` one-liner the README
   quickstart is built on, because the reader would need a PAT before they could run it.
2. **Use Trusted Publishing (OIDC), not an API key secret.** NuGet.org has supported it since
   September 2025 via the first-party `NuGet/login@v1` action. More decisively: since
   **17 August 2026 new nuget.org API keys are capped at 30 days** (§3.2), so the
   "long-lived secret" option no longer really exists — it is now a 30-day rotation chore.
3. **Do not add `actions/setup-dotnet` for the SDK itself.** The .NET 10 SDK is preinstalled
   on both `ubuntu-latest` and `windows-latest`, in four feature bands up to 10.0.400 (§2.2).
4. **If you add a `global.json`, set `rollForward` explicitly.** The default when you pin a
   version and omit `rollForward` is **`patch`** — the narrowest policy there is (§2.4). This
   is the single most likely way to break CI on a runner-image update.
5. **All four candidate package IDs are free, and `dotnet-reach` is safe to push** (§4.1,
   §4.3). But note the decoupling proved in §4.2: `dotnet reach` comes from
   `ToolCommandName`, **not** from the package ID. The package could be called `Reach` and
   still be invoked as `dotnet reach`.
6. **The prerelease decision changes the README's first line.** `dnx dotnet-reach` will
   **not** find `0.1.0-alpha.1` — it fails with a misleading "not found in NuGet feeds"
   (§1.4). The quickstart must either pin `@0.1.0-alpha.1` (which works, and is already the
   form [tool packaging](03-tool-packaging-and-cli-library.md) recommended) or ship 1.0.0.
7. **`dotnet test` is still VSTest by default in .NET 10.** The opt-in to
   Microsoft.Testing.Platform is `global.json`, **not** `dotnet.config` (§5). A plain
   `dotnet test --logger trx` CI step keeps working; nothing needs deciding here for M1.

---

## 1. `dnx` and which feeds it resolves from

### 1.1 `dnx` is `dotnet tool exec` wearing a hat

Three layers, all confirmed:

- **A shim on PATH.** On this machine, `C:\Program Files\dotnet\dnx.cmd` is four lines, of
  which one matters:

  ```bat
  @echo off
  "%~dp0dotnet.exe" dnx %*
  ```

- **A hidden `dotnet` subcommand**, `dotnet dnx`. In the SDK source it is declared
  `Hidden = true` and does nothing but delegate
  (`https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Commands/Dnx/DnxCommandParser.cs`):

  ```csharp
  command.SetAction(parseResult => new ToolExecuteCommand(parseResult).Execute());
  ```

- **`dotnet tool exec`**, the real implementation. The docs say so outright: "`dotnet dnx` -
  A hidden alias for `dotnet tool exec`"
  (`https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec`).

Verified on the machine rather than taken on trust — `dotnet dnx --help` and
`dotnet tool exec --help` print **byte-identical option lists**, and the latter's usage line
reads `dotnet tool execute <packageId>`. Requires the **.NET 10.0.100 SDK or later**.

The option set, verbatim from `dotnet dnx --help` on 10.0.303:

```
--version <VERSION>       The version of the tool package to install.
--allow-roll-forward      Allow a .NET tool to roll forward to newer versions of the .NET runtime...
--prerelease              Include pre-release packages. [default: False]
--configfile <FILE>       The NuGet configuration file to use.
--source <SOURCE>         Replace all NuGet package sources to use during installation with these.
--add-source <ADDSOURCE>  Add an additional NuGet package source to use during installation.
--ignore-failed-sources   Treat package source failures as warnings. [default: False]
--interactive             Allows the command to stop and wait for user input or action...
```

Note `--source` **replaces** all sources; `--add-source` appends. Neither `--yes` nor `-y`
appears in help output — it exists in the SDK source but is declared `hidden: true`
(`ToolExecuteCommandDefinition.cs`). Passing `--yes` after the package id is silently
forwarded to the tool as a command argument, not consumed by `dnx` (§1.5).

### 1.2 It honours the ambient NuGet config — there is no hardcoded nuget.org

The docs state the rule — "If not specified, the hierarchy of configuration files from the
current directory will be used" (`dotnet tool exec` docs) — and two experiments settle it.

**Experiment A — clear all sources.** A scratch directory containing only:

```xml
<configuration>
  <packageSources>
    <clear />
  </packageSources>
</configuration>
```

```
$ dotnet dnx dotnetsay
No NuGet sources are defined or enabled
(exit 1)
```

If nuget.org were baked in, this would have succeeded. It did not.

**Experiment B — a directory-level feed only.** Same directory, `<clear />` plus a single
local folder source containing `dotnetsay.3.0.3.nupkg`:

```
$ dotnet dnx dotnetsay
    __________________
                      \
                       \
                          ....
```

It resolved and ran entirely offline from a folder feed, with nuget.org cleared.

**Conclusion for the ticket:** `dnx` will resolve from *any* configured feed, including
GitHub Packages, **if** a `nuget.config` on the resolution path (or `--add-source`) names it
and supplies credentials. It just never finds one by itself.

### 1.3 GitHub Packages requires a token even for public packages

This is the decisive constraint, and it is stated flatly in GitHub's own docs
(`https://docs.github.com/en/packages/learn-github-packages/about-permissions-for-github-packages`):

> In most registries, to pull a package, you must authenticate with a personal access token
> or `GITHUB_TOKEN`, **regardless of whether the package is public or private**. However, in
> the Container registry, public packages allow anonymous access and can be pulled without
> authentication.

NuGet is explicitly in the "most registries" group; the Container registry (`ghcr.io`) is the
*sole* documented exception. The NuGet-specific page repeats it
(`https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry`):
"You need an access token to publish, install, and delete **private, internal, and public
packages**."

Confirmed live, unauthenticated, from this machine:

| Endpoint | Result |
| --- | --- |
| `https://nuget.pkg.github.com/dotnet/index.json` | **HTTP 401** |
| `https://nuget.pkg.github.com/github/index.json` | **HTTP 401** |
| `https://nuget.pkg.github.com/actions/index.json` | **HTTP 401** |

The service index itself — before any package is named — refuses anonymous callers.

Token scopes (same GitHub source): `read:packages` to install, `write:packages` to publish,
`delete:packages` to delete. **Only classic PATs are supported** ("GitHub Packages only
supports authentication using a personal access token (classic)"). Within the workflow's own
repository, `GITHUB_TOKEN` covers both publish and restore.

**What this means for Reach.** Publishing to GitHub Packages is fine for *Reach's own CI*
(`GITHUB_TOKEN` just works in-repo). It is unusable as the **public** distribution channel,
because PRD §1.2's "introduced in an afternoon, on a repository they have never seen" would
become "first, go and mint a classic PAT". The README's `dnx` line only works against
nuget.org or a feed the reader already has.

### 1.4 A prerelease-only package is invisible to `dnx`

Built a real tool package — `PackAsTool`, `ToolCommandName=dotnet-faketool`,
`Version=0.1.0-alpha.1` — dropped it into a local folder feed, and pointed a scratch
`nuget.config` at that feed only.

| Invocation | Result |
| --- | --- |
| `dotnet dnx dotnet-faketool` | **`dotnet-faketool is not found in NuGet feeds <path>.`** (exit 1) |
| `dotnet dnx dotnet-faketool --prerelease` | `FAKETOOL RAN OK` (exit 0) |
| `dotnet dnx dotnet-faketool@0.1.0-alpha.1` | `FAKETOOL RAN OK` (exit 0) |

Two things worth carrying into the docs ticket:

- The failure message is **actively misleading**. The package *is* in the feed. A reader who
  hits this will think the publish failed, not that they need a flag. Any quickstart that
  names a bare package id while only prereleases exist is a support burden.
- **Pinning the exact version sidesteps `--prerelease` entirely.** `@0.1.0-alpha.1` resolved
  without the flag. This is lucky: [tool packaging](03-tool-packaging-and-cli-library.md)
  already recommended `dnx dotnet-reach@<version>` as the headline instruction, so that
  guidance survives a prerelease M1 unchanged. A bare `dnx dotnet-reach` does not.

The same stable-only default applies to `dotnet tool install`, whose `--version` doc reads
"By default, the latest stable package version is installed."

### 1.5 `dnx` does not block on a prompt in CI

The .NET 10 What's New page says "By default, users are prompted to confirm the download if
the tool doesn't already exist locally", which would be a hang risk for CI. Tested against
nuget.org with a package never used on this machine, stdin not a TTY:

```
$ dotnet dnx dotnet-counters@9.0.553101 -- --version
9.0.553101+5b61d34de04d6100e6003415f7d7e9c4b971afd4
(exit 0, 3.9s)
```

No prompt, no hang. The hidden `--yes` flag exists if it is ever needed. **Caveat:** I
observed this only with redirected stdin on Windows; I did not verify the prompt's exact
trigger condition in SDK source, so "it never prompts in CI" is an observation on one
platform, not a proof.

---

## 2. .NET 10 on GitHub-hosted runners

### 2.1 What the `-latest` labels point at

From `https://github.com/actions/runner-images/blob/main/README.md`:

| Image | Label |
| --- | --- |
| Ubuntu 24.04 | `ubuntu-latest`, `ubuntu-24.04` |
| Windows Server 2025 | `windows-latest`, `windows-2025` |
| Windows Server 2022 | `windows-2022` (no longer `-latest`) |

The README also warns that `-latest` moves: "The `-latest` migration process is gradual and
happens over 1-2 months in order to allow customers to adapt their workflows."

### 2.2 .NET 10 is preinstalled, in four feature bands

Identical `.NET Core SDK` lists on both `Ubuntu2404-Readme.md` and `Windows2025-Readme.md`
(`https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md`,
`.../images/windows/Windows2025-Readme.md`), verbatim:

> 8.0.130, 8.0.206, 8.0.319, 8.0.424, 9.0.120, 9.0.205, 9.0.317, **10.0.111, 10.0.204,
> 10.0.303, 10.0.400**

So bands **10.0.1xx, 10.0.2xx, 10.0.3xx and 10.0.4xx** are all present, and `dotnet` with no
`global.json` picks the highest — 10.0.400. .NET 10 GA'd **11 November 2025**
(`https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md`); the SDK landed in
the Ubuntu image on 17 November 2025 and the Windows images on 4 December 2025.

**`actions/setup-dotnet` is therefore not needed** to get a .NET 10 SDK, which matters
because [issue 20](20-what-documentation-m1-ships.md) makes the workflow a documentation
artifact — every line a reader does not have to understand is a win. Current version is
**v6** (released 2026-07-16); v5 dates from 2025-09-03.

### 2.3 `setup-dotnet` does not read `global.json` unless you tell it to

From the action's README
(`https://github.com/actions/setup-dotnet/blob/main/README.md`): "Input `global-json-file` is
used for specifying the path to the `global.json`. If the file that was supplied to
`global-json-file` input doesn't exist, the action will fail with error." There is no
auto-discovery. Also: "In case both `dotnet-version` and `global-json-file` inputs are used,
versions from both inputs will be installed."

The distinction that matters: the **action** ignores an un-named `global.json`, but the
`dotnet` muxer does not — once the step runs, any `dotnet` invocation walks up the directory
tree and honours whatever `global.json` it finds. So a `global.json` in the repo affects CI
whether or not `setup-dotnet` is in the workflow.

### 2.4 `global.json` roll-forward — the default is the trap

From `https://learn.microsoft.com/en-us/dotnet/core/tools/global-json`. `sdk.version`
"Requires the full version number, such as 10.0.100... Doesn't have wildcard support."

The policies:

| `rollForward` | Behaviour |
| --- | --- |
| `patch` | Specified version, else latest patch **in the same feature band**, else fail |
| `feature` | Latest patch in band, else next higher feature band in same major.minor, else fail |
| `minor` | …then next higher minor within the same major, else fail |
| `major` | …then next higher major, else fail |
| `latestPatch` / `latestFeature` / `latestMinor` / `latestMajor` | Highest installed at that scope, ≥ specified |
| `disable` | Exact match only |

And the default, quoted verbatim:

> If a *global.json* file is found and it specifies an SDK version: If no `rollForward` value
> is set, it uses `patch` as the default `rollForward` policy.

**`patch` is the narrowest policy in the table.** A `global.json` pinning, say, `10.0.500`
would fail on today's runners outright — none of 10.0.111/204/303/400 is in the 5xx band.
Pinning `10.0.100` happens to survive today (rolls to 10.0.111), but only because the image
still carries a 1xx SDK; images drop old bands over time. Every row ends "else fail", and the
failure is the familiar `A compatible .NET SDK was not found`.

If Reach ships a `global.json` at all, `rollForward` should be stated explicitly —
`latestFeature` or `latestMinor` keeps the .NET 10 floor that
[ADR-0013](../../../docs/adr/0013-the-tool-targets-net10-0.md) fixes, without pinning to a
band that will age out of the runner image.

With **no** `global.json`, or one with no `sdk.version`, the muxer simply takes the highest
installed SDK. For a repo whose only requirement is "a .NET 10 SDK", omitting `global.json`
entirely is a defensible M1 answer — the runner images already guarantee it.

---

## 3. Publishing to NuGet.org

### 3.1 Trusted Publishing (OIDC) exists and is first-party

Announced **22 September 2025**
(`https://devblogs.microsoft.com/dotnet/enhanced-security-is-here-with-the-new-trust-publishing-on-nuget-org/`),
documented at `https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing`. It
works for GitHub Actions and GitLab CI; Azure DevOps is **not** yet supported
(`https://github.com/NuGet/NuGetGallery/issues/10957`, open).

How it works, per the docs: the workflow requests an OIDC token from GitHub, nuget.org's
token-exchange endpoint checks it against a policy you configured, and issues a **short-lived
API key valid for 1 hour**. "Each short-lived token can only be used once to obtain a single
temporary API key—one token, one API key."

On nuget.org you register: Repository Owner, Repository, **Workflow File** (file name only,
e.g. `release.yml`), and optionally Environment.

The workflow, verbatim from the docs:

```yaml
jobs:
  build-and-publish:
    permissions:
      id-token: write  # enable GitHub OIDC token issuance for this job

    steps:
      # Build your artifacts/my-sdk.nupkg package here

      - name: NuGet login (OIDC -> temp API key)
        uses: NuGet/login@v1
        id: login
        with:
          user: contoso-bot

      - name: NuGet push
        run: dotnet nuget push artifacts/my-sdk.nupkg --api-key ${{steps.login.outputs.NUGET_API_KEY}} --source https://api.nuget.org/v3/index.json
```

`permissions: id-token: write` is required; without it the OIDC request fails. The `user`
input is the nuget.org **profile name**, not an email.

One gotcha to design around: a newly created policy is "temporarily active for **7 days**...
if no publish happens within those 7 days, the policy automatically becomes inactive." So the
policy should be created close to the first real publish, not months ahead.

### 3.2 API keys are now capped at 30 days — this is what decides it

Announced 3 August 2026
(`https://devblogs.microsoft.com/dotnet/strengthening-nuget-supply-chain-security-reducing-api-key-lifetime/`):

> Starting August 17, 2026, new API keys will be limited to 30 days. All existing API keys
> created before that date will expire on November 1, 2026.

As of today the maximum lifetime for a **new** nuget.org API key is **30 days**. The
"long-lived API key in a GitHub secret" option is effectively gone — choosing it commits
someone to rotating a secret twelve times a year, forever, on a hobby project. Trusted
Publishing is not merely the recommended path now; it is the only one that does not decay.

(Note: `https://learn.microsoft.com/en-us/nuget/nuget-org/scoped-api-keys` still shows a
365-day example. That page has not caught up with the August 2026 change; the blog post is
authoritative.)

Keys are scoped by glob pattern over package IDs and by action (push new packages / push new
versions only / unlist). `dotnet nuget push` takes `--api-key`, `--source`, and
`--skip-duplicate` ("treats any 409 Conflict response as a warning"). Since NuGet 7.6 /
SDK 10.0.300 a `NUGET_API_KEY` environment variable can replace `--api-key`.

### 3.3 Signing is not required

- **Author signing is optional.** No nuget.org policy requires it, and it is "only supported
  by nuget.exe on Windows at this time"
  (`https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference`).
- **Repository signing is automatic**: "all packages uploaded to nuget.org are automatically
  repository signed" (same source). Reach gets this for free.
- **One sharp edge if you ever opt in:** once an account registers *any* certificate, "all
  package submissions **must** be signed with one of the certificates"
  (`https://learn.microsoft.com/en-us/nuget/create-packages/sign-a-package`). Author signing
  is opt-in but sticky at the account level, so it is not a decision to take casually on a
  whim.

No 2026 policy change mandating author signatures was found.

### 3.4 Prereleases

SemVer 2.0.0 is supported (NuGet 4.3.0+). `0.1.0-alpha.1` uses a dot-separated prerelease
label, which the docs call out specifically: "Prerelease numbers with dot notation, as in
*1.0.1-build.23*, are considered part of the SemVer 2.0.0 standard, and as such are only
supported with NuGet 4.3.0+"
(`https://learn.microsoft.com/en-us/nuget/concepts/package-versioning`). nuget.org flags such
packages "SemVer v2.0.0" and they are invisible to pre-4.3.0 clients — irrelevant for Reach,
whose floor is already a .NET 10 SDK.

Two version-hygiene notes from the same page: prerelease comparison is **case-insensitive**
(`1.0.0-alpha` == `1.0.0-Alpha`), and numeric suffixes sort as strings, so `beta10` sorts
before `beta2` — use `beta02` if a two-digit series is ever likely.

The consumer-side consequence is §1.4: stable-only by default, everywhere.

### 3.5 ID prefix reservation, and what a reserved prefix actually blocks

From `https://learn.microsoft.com/en-us/nuget/nuget-org/id-prefix-reservation`. Applying is an
email to `account@nuget.org` naming the owner display name and the prefixes wanted. Criteria
include "Does the package ID prefix properly and clearly identify the reservation owner?" and
"Avoid ID prefix reservations that are shorter than four characters and avoid common or
generic words."

The behaviour that matters, verbatim:

> Whenever a package is submitted to nuget.org with an ID that matches the reserved ID prefix,
> **the package is rejected unless it originates from the owner(s) that reserved the ID
> prefix.**

So a reserved prefix is a hard push-time block, not just a missing checkmark. Packages that
predate a reservation are grandfathered. See §4.3 for whether this bites `dotnet-reach`.

### 3.6 Other publishing mechanics

- **Indexing delay:** "Package validation and indexing usually take less than 15 minutes"
  (`https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package`). A release workflow
  that publishes and then immediately smoke-tests `dnx dotnet-reach@<version>` will be racing
  this. Either wait or retry.
- **README and license are optional**, both listed under optional metadata in the `.nuspec`
  reference. `licenseUrl` is deprecated in favour of `<license>`; nuget.org "only accepts
  license expressions that are approved by the Open Source Initiative or the Free Software
  Foundation". Required elements are only `id`, `version`, `description`, `authors`.
- **You cannot delete.** "nuget.org does not support permanent deletion of packages"
  (`https://learn.microsoft.com/en-us/nuget/nuget-org/policies/deleting-packages`). Unlisting
  is self-service, hides the package from search, but "can still be downloaded and installed
  by using an exact version number". **A bad `0.1.0-alpha.1` is permanent** — which is an
  argument for keeping M1's first published version deliberately low-stakes.
- **ID rules:** ≤100 characters, must start with a letter/number/underscore, only letters,
  numbers, dots and dashes, no consecutive separators.

---

## 4. Package ID availability and the `dotnet-` prefix

### 4.1 All four candidates are free

Checked against four independent nuget.org endpoints per ID — the V3 registration API, the
flat container, the package page, and the search API's exact `packageid:` filter.

| ID | `registration5-semver1` | `v3-flatcontainer` | `nuget.org/packages/<id>` | `packageid:` search |
| --- | --- | --- | --- | --- |
| `dotnet-reach` | 404 | 404 | 404 | `totalHits: 0` |
| `Reach` | 404 | 404 | 404 | `totalHits: 0` |
| `DotnetReach` | 404 | 404 | 404 | `totalHits: 0` |
| `Reach.Cli` | 404 | 404 | 404 | `totalHits: 0` |

**All four are unregistered as of 11 September 2026.** A general search for `reach` turned up
only unrelated near-misses (`Epicalsoft.Reach.Api.Client.Net`, `reachmail`, `ReachOut.Jwt`,
`Reachability.Net`) — nothing that would cause confusion.

One caution on `Reach` as an ID: the prefix-reservation criteria explicitly say to "avoid
common or generic words", so `Reach` is a name Reach could publish but probably could never
*reserve*.

### 4.2 `dotnet reach` comes from `ToolCommandName`, not the package ID

The docs state the rule: "If the command begins with the prefix `dotnet-`, an alternative way
to invoke the tool is to use the `dotnet` command and omit the tool command prefix"
(`https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools`) — already quoted in
[tool packaging](03-tool-packaging-and-cli-library.md) §2. What was *not* established there,
and matters for the ID decision, is whether the **package ID** has to match.

It does not. Built a second tool package with the two deliberately divergent:

```xml
<PackageId>ReachTestPkg</PackageId>
<ToolCommandName>dotnet-faketool2</ToolCommandName>
```

```
$ dotnet tool install ReachTestPkg --tool-path <scratch> --prerelease
You can invoke the tool using the following command: dotnet-faketool2
Tool 'reachtestpkg' (version '0.1.0-alpha.1') was successfully installed.

$ ls <scratch>
dotnet-faketool2.exe

$ dotnet faketool2
PACKAGEID-IS-ReachTestPkg BUT VERB WORKED
```

The shim on PATH is named after `ToolCommandName`; the `dotnet` muxer finds `dotnet-<verb>`
on PATH; the package ID is never consulted. **So the naming decision splits in two:**

| Choice | Governs |
| --- | --- |
| `ToolCommandName` | Whether `dotnet reach` works. Must be `dotnet-reach`. |
| `PackageId` | What the reader types in `dnx <id>` and `dotnet tool install <id>`. |

Publishing as `Reach` with `ToolCommandName=dotnet-reach` would give `dotnet reach` **and**
a `dnx Reach` install line. Publishing as `dotnet-reach` makes both the same string. The
latter is more conventional and keeps the docs free of a "the package is called X but the
command is Y" footnote — but it is now a choice, not a constraint.

Relatedly, a .NET 10 breaking change worth knowing: "The `ToolCommandName` property is no
longer set automatically for all projects during build or package operations. It's now only
set when `PackAsTool` is set to `true`"
(`https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/toolcommandname-not-set`).

For **local** tools the CLI prints both forms: "You can invoke the tool from this directory
using the following command: 'dotnet tool run dotnet-env' or 'dotnet dotnet-env'", and
`dotnet <verb>` also works.

### 4.3 `dotnet-` is not blanket-reserved, and `dotnet-reach` would not be rejected

nuget.org does not publish the list of reserved prefixes, so this is settled by evidence
rather than by lookup. If `dotnet-` were reserved to Microsoft, no third party could hold a
`dotnet-*` ID. Many do:

| Package | Owner | Reserved-prefix badge |
| --- | --- | --- |
| `dotnet-ef` | Microsoft, aspnet | **Yes** |
| `dotnet-trace` | Microsoft | **No** |
| `dotnet-dump` | Microsoft | **No** (`"verified": false`) |
| `dotnet-stryker` | stryker-mutator | **Yes** |
| `dotnet-sonarscanner` | SonarSource | **Yes** |
| `dotnet-outdated-tool` | dotnet-outdated | **No** |
| `dotnet-serve` | natemcmaster | **No** |
| `dotnet-script` | bernhard richter, filipw | **No** |
| `dotnet-reportgenerator-globaltool` | danielpalme | **No** |

Two things follow. Third parties (`natemcmaster`, `danielpalme`, SonarSource,
stryker-mutator) own and actively publish `dotnet-*` IDs. And **Microsoft's own**
`dotnet-trace` and `dotnet-dump` carry no reserved-prefix badge — which they would if
Microsoft held the `dotnet-` stem. The reservations that exist are on specific full IDs
(`dotnet-ef`, `dotnet-stryker`, `dotnet-sonarscanner`), not on the stem.

Supporting this from the other direction, §1.5's experiment pulled `dotnet-outdated-tool`
4.8.1 — a community-owned `dotnet-` package — straight off nuget.org.

**Conclusion: pushing `dotnet-reach` would not be blocked.** Stated honestly, this is a
strong inference from the absence of any conflicting reservation plus the demonstrated
pattern, not a guarantee from a published list — see "What I could not establish". The
cheapest way to remove the doubt entirely is to push `0.1.0-alpha.1` early and find out, which
is a further argument for publishing something small in M1 rather than saving the first push
for a release that matters.

No Microsoft guidance discouraging the `dotnet-` prefix was found; the global-tools docs
effectively encourage it, since it is the documented mechanism for the `dotnet <verb>`
shorthand.

---

## 5. `dotnet test` in .NET 10

### 5.1 VSTest is still the default

Confirmed on this machine. `dotnet test --help` on SDK 10.0.303, first line:

> .NET Test Command for VSTest. To use Microsoft.Testing.Platform, opt-in to the
> Microsoft.Testing.Platform-based command via **global.json**.

And in the docs: "VSTest mode: This is the default mode for `dotnet test`... MTP mode:
Introduced with the .NET 10 SDK"
(`https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test`).

### 5.2 The opt-in is `global.json`, not `dotnet.config`

The ticket's premise named `dotnet.config` with a `[dotnet.test.runner]` section. **That
mechanism does not exist**, at least not in SDK 10.0.303. Tested directly:

```toml
# dotnet.config in the working directory
[dotnet.test.runner]
name = "Microsoft.Testing.Platform"
```

`dotnet test --help` still reported "**.NET Test Command for VSTest**". Retried with an
unquoted value — same result. Searching the SDK's own command definitions
(`C:\Program Files\dotnet\sdk\10.0.303\Microsoft.DotNet.Cli.Definitions.xml`) finds **zero**
occurrences of `dotnet.config`, `dotnet.test.runner`, or `testingPlatformDotnetTestSupport`,
while the surrounding help strings are present — so the file is genuinely not consulted.

What *does* work, verified:

```json
{
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

With that `global.json` present, the same command reports:

> .NET Test Command for Microsoft.Testing.Platform (opted-in via 'global.json' file). This
> only supports Microsoft.Testing.Platform and doesn't support VSTest.

This matches the docs: "Starting with .NET 10 SDK, there is native support for MTP. To use
it, you must specify the test runner as `Microsoft.Testing.Platform` in *global.json*".
Requires MTP 1.7+.

The older `TestingPlatformDotnetTestSupport` MSBuild property is a *different*, pre-.NET-10
bridge that let VSTest-mode `dotnet test` drive MTP apps. It is legacy: "The support of
running under this mode will be removed in MTP version 2 if run with .NET 10 SDK."

### 5.3 The flag surface changes completely under MTP

This is the part that decides what a CI step looks like. Under MTP mode on 10.0.303, the
build-shaped flags survive — `-c|--configuration`, `-f|--framework`, `--no-build`,
`--no-restore`, `--artifacts-path`, `-e`, `-v`, `--results-directory` — but the two flags a
CI test step actually reaches for are **gone from the built-in set**:

| VSTest | MTP equivalent | Requires |
| --- | --- | --- |
| `--logger trx` | `--report-trx` | `Microsoft.Testing.Extensions.TrxReport` package reference |
| `--collect "XPlat Code Coverage"` | `--coverage --coverage-output-format cobertura` | `Microsoft.Testing.Extensions.CodeCoverage` |
| `<project.dll>` positional | `--test-modules` / `--project` / `--solution` | — |

The docs are explicit about why: "`--collect` is a general extensibility point in VSTest...
The extensibility model of MTP is different and there is no such centralized argument"
(`https://learn.microsoft.com/en-us/dotnet/core/testing/migrating-vstest-microsoft-testing-platform`).
The MTP help output ends with `Waiting for options and extensions...`, i.e. the remaining
options are discovered from the test binary, not fixed by the SDK.

Three further MTP behaviours that would surprise a CI author:

- A project that runs **zero tests fails** with exit code 8 (VSTest succeeds).
- A missing extension yields **exit code 5** (unrecognized option), not a clear error.
- "When MTP is opted in via `global.json`, `dotnet test` expects all test projects to use
  MTP. It is an error if any of the test projects use VSTest." — all-or-nothing per repo.

**Bearing on this ticket:** M1 needs no decision here. Doing nothing leaves VSTest, where
`dotnet test --logger trx --configuration Release --no-build` behaves exactly as it always
has. The trap to avoid is adding `"test": {"runner": ...}` to a `global.json` added for
§2.4's SDK-pinning reasons and silently changing the test contract at the same time — the two
concerns share a file but should not share a decision.

---

## What I could not establish

- **Whether `dotnet-reach` is literally pushable.** nuget.org publishes no list of reserved
  prefixes, so §4.3 is inference from nine sampled packages plus the absence of a conflicting
  ID — strong, but not a guarantee. Only an actual push settles it.
- **Whether GitHub's "auth required even for public NuGet packages" rule is long-standing or
  a recent change.** The current docs are unambiguous (§1.3) and the live 401s confirm
  today's behaviour, but no `github.blog/changelog` entry was found announcing it either way,
  and Wayback Machine lookups for historical doc wording were rate-limited (HTTP 429). The
  *current* state is certain; its history is not.
- **`dnx`'s exact prompt-trigger condition.** §1.5 observed no prompt with redirected stdin
  on Windows; I did not read the SDK source to determine when the documented confirmation
  prompt actually fires. "Safe in CI" rests on one platform's observation.
- **Whether `dotnet.config` exists in a later SDK feature band.** §5.2 disproves it for
  **10.0.303**, which is what is installed here. The runner images also carry **10.0.400**
  (§2.2), which I could not test. The docs describe only the `global.json` mechanism, so I do
  not think this is a version gap — but the negative result is scoped to 10.0.303.
- **Whether `GITHUB_TOKEN` can read a *public* package from a different repository.** The
  docs address the private-repo case explicitly and the cross-repo grant mechanism, but not
  this exact combination. Moot if publishing goes to nuget.org.
- **Whether Trusted Publishing is formally "GA" or still preview.** The feature is live,
  documented, and a year old, but neither the announcement nor the docs page uses the words
  "generally available".

---

## Sources

`dnx` and tool execution
- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec>
- <https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/sdk>
- <https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dnx-ps1-removed>
- <https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Commands/Dnx/DnxCommandParser.cs>
- <https://learn.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior>
- `C:\Program Files\dotnet\dnx.cmd`

GitHub Packages
- <https://docs.github.com/en/packages/learn-github-packages/about-permissions-for-github-packages>
- <https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry>

Runners and SDK selection
- <https://github.com/actions/runner-images/blob/main/README.md>
- <https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md>
- <https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md>
- <https://github.com/actions/setup-dotnet/blob/main/README.md>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/global-json>
- <https://github.com/dotnet/core/blob/main/release-notes/10.0/README.md>

NuGet.org publishing
- <https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing>
- <https://devblogs.microsoft.com/dotnet/enhanced-security-is-here-with-the-new-trust-publishing-on-nuget-org/>
- <https://devblogs.microsoft.com/dotnet/strengthening-nuget-supply-chain-security-reducing-api-key-lifetime/>
- <https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package>
- <https://learn.microsoft.com/en-us/nuget/nuget-org/id-prefix-reservation>
- <https://learn.microsoft.com/en-us/nuget/concepts/package-versioning>
- <https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference>
- <https://learn.microsoft.com/en-us/nuget/create-packages/sign-a-package>
- <https://learn.microsoft.com/en-us/nuget/nuget-org/policies/deleting-packages>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push>

Tool naming
- <https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use>
- <https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/toolcommandname-not-set>
- `https://api.nuget.org/v3/registration5-semver1/{dotnet-reach,reach,dotnetreach,reach.cli}/index.json`
- `https://azuresearch-usnc.nuget.org/query?q=packageid:...&prerelease=true&semVerLevel=2.0.0`

`dotnet test`
- <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test>
- <https://learn.microsoft.com/en-us/dotnet/core/testing/migrating-vstest-microsoft-testing-platform>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp>
- `C:\Program Files\dotnet\sdk\10.0.303\Microsoft.DotNet.Cli.Definitions.xml`
