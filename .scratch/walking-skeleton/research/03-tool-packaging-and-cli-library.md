# Tool packaging and CLI library

Research for [issue 03](../issues/03-tool-packaging-and-cli-library.md). Investigated 5 September 2026.

Machine used for the empirical checks: Windows 11, .NET SDK 10.0.303, with
`Microsoft.NETCore.App` 3.1.32, 5.0.17, 6.0.36, 8.0.30, 9.0.19 and 10.0.11 installed
(`dotnet --list-runtimes`). **.NET 7 is not installed**, which is what makes the
roll-forward experiment below a clean analogue of "the tool targets an older major than
anything on the agent".

Nothing in the repository was built or modified for this. Every experiment ran against
packages downloaded from nuget.org into a scratch directory.

---

## Recommendations

1. **Use `System.CommandLine` 2.0.x.** Its API has genuinely stabilised: 2.0.0 shipped
   stable on 11 November 2025 alongside .NET 10, it has had twelve servicing patches on a
   monthly cadence since, the `dotnet` CLI in the installed SDK is itself built on
   2.0.11, and the Microsoft Learn documentation no longer carries a preview warning. On
   `net8.0` it has **zero package dependencies** and a 151 KB assembly. See §1.
2. **Do not treat the 3.0.0 preview line as churn.** 3.0 is a version bump riding the
   .NET 11 train, not a redesign — its milestone contains eight items, all additive or
   test-tooling fixes. 2.0.x continues to be serviced in parallel. See §1.3.
3. **Set `<RollForward>LatestMajor</RollForward>` and confirm it lands in the packed
   `runtimeconfig.json`.** Without it the tool *will* fail on an agent whose only runtime
   is a newer major — proven in §3.3. With it, a `net8.0`-only tool installs under the
   .NET 10 SDK and runs on .NET 10.0.11 — also proven, end to end, in §3.4.
4. **For CI, prefer `dotnet tool exec` (a.k.a. `dnx`) as the documented path, with a
   local tool manifest as the pinned alternative.** Avoid `-g|--global`. See §4.
5. **Nothing about tool packaging constrains which assemblies Reach can read** (§5) — but
   a *different* constraint on `net8.0` did turn up: **Roslyn dropped its `net8.0` asset
   at `Microsoft.CodeAnalysis.CSharp` 5.9.0**. This is the one finding that could push the
   target framework decision. See §5.2. It needs a decision, not a research answer.

---

## 1. Has `System.CommandLine`'s public API actually stabilised?

**Yes.** This is the strongest-evidenced part of the ticket.

### 1.1 Release record

Every stable version and its publish timestamp, from the NuGet registration API
(`https://api.nuget.org/v3/registration5-semver1/system.commandline/index.json`):

| Version | Published (UTC) |
| --- | --- |
| 2.0.0 | 2025-11-11 |
| 2.0.1 | 2025-12-09 |
| 2.0.2 | 2026-01-13 |
| 2.0.3 | 2026-02-10 |
| 2.0.4 | 2026-03-10 |
| 2.0.5 | 2026-03-12 |
| 2.0.6 | 2026-04-14 |
| 2.0.7 | 2026-04-21 |
| 2.0.8 | 2026-05-12 |
| 2.0.9 | 2026-06-09 |
| 2.0.10 | 2026-07-14 |
| 2.0.11 | 2026-08-11 |

The GitHub releases API (`https://api.github.com/repos/dotnet/command-line-api/releases`)
confirms `v2.0.0` was published `2025-11-11T15:14:16Z` with `"prerelease": false` — the
same day .NET 10 shipped. The prior tag, `v2.0.0-rc.2.25502.107`, is marked
`"prerelease": true`.

Read that table as a shape, not a list: a GA, then **monthly** patch releases on the .NET
servicing dates for ten months straight, plus two off-cycle fixes (2.0.5, 2.0.7). That is
a product on a release train, not a long-running beta. The contrast with its own history
is the point — the package sat on `2.0.0-beta1.*` through `2.0.0-beta4.22272.1` from 2020
to **June 2022**, then went silent for three years until `2.0.0-beta5.25306.1` in June
2025. The churn the ticket is worried about is real, but it is *over*, and it ended at a
dated, identifiable event.

Source: `https://api.nuget.org/v3-flatcontainer/system.commandline/index.json`,
`https://github.com/dotnet/command-line-api/releases`.

### 1.2 It is a shipping .NET product component, not a side project

Four independent confirmations:

- **The .NET 10 GA release notes list it as a shipped package.** The packages table in
  `https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.0/10.0.0.md` contains
  `System.CommandLine | 2.0.0`.
- **The SDK on this machine ships it and uses it.**
  `C:\Program Files\dotnet\sdk\10.0.303\System.CommandLine.dll` has
  `ProductVersion = 2.0.11+e2f47b0110ed922f21a1522da67279133ce28f32`. The `dotnet` CLI
  itself is built on the exact version under evaluation.
- **It is built out of the VMR.** The 2.0.11 `.nuspec` declares
  `<repository type="git" url="https://github.com/dotnet/dotnet" commit="e2f47b01…" />` —
  the same commit hash as the SDK's copy. It is compiled as part of the .NET product
  build, so it inherits the product's servicing and API-review discipline.
- **The docs no longer warn about preview status.** The overview at
  `https://learn.microsoft.com/en-us/dotnet/standard/commandline/` (updated 2025-12-04)
  describes it plainly, notes it is trim- and AOT-friendly, and states "Apps that use
  `System.CommandLine` include the .NET CLI". Earlier revisions of this page carried an
  explicit prerelease banner; it is gone.

### 1.3 What about the 3.0.0 preview line?

There is a 3.0 preview series, and it deserves an honest look rather than being waved
away — but it is not evidence of instability.

Previews `3.0.0-preview.1.26104.118` (2026-02-10) through `3.0.0-preview.7.26381.103`
(2026-08-11) were published on **exactly the same dates** as 2.0.3 … 2.0.11. The two lines
are shipping side by side: 3.0 previews with the .NET 11 previews, 2.0.x servicing with
.NET 10. 2.0.x is not abandoned — 2.0.11 and 3.0.0-preview.7 came out the same day.

The 3.0.0 milestone (`https://api.github.com/repos/dotnet/command-line-api/milestones`,
due `2026-11-10`, i.e. .NET 11 GA) holds **8 issues, 1 closed**:

- Option with default value does not have Action added to PreActions *(closed)*
- Missing support for Finnish language
- add `Uri` to `ArgumentConverter.StringConverters`
- Create new Samples project with strong focus on best practices
- When Description is empty, the description section should be omitted from help
- API summarizer for ApprovalTests does not capture generic constraints
- API summarizer for ApprovalTests shows `ref` for `out` parameters
- API summarizer for ApprovalTests does not show `override` methods correctly

Three of those eight are fixes to the repo's *own public-API approval test tooling* —
which is itself a stability signal: the project now gates its public surface behind
approved API baselines. Nothing in the milestone is an API redesign. The major version
bump tracks the .NET major, as out-of-band .NET libraries do.

**Practical read:** taking 2.0.x for M1 carries no known forced migration. 2.0 shipped
with .NET 10, an LTS release, so it will be serviced for that release's lifetime.

**What I could not establish:** I found no explicit, written post-GA API-compatibility
commitment from the team. Issue
`https://github.com/dotnet/command-line-api/issues/2576` sets out the path to GA
("Our objective is to publish a stable (non-preview) release of System.CommandLine 2.0.0
around the same time .NET 10 ships in November 2025") and states that the beta5 breaking
changes "were scoped to those with a favorable value to disruption comparison", but it
does not say in so many words "the API is frozen after 2.0". The inference that it is
stable rests on the shipping evidence above, not on a promise. That is a weaker form of
evidence and should be recorded as such.

### 1.4 Dependency footprint

From the packages themselves (uncompressed sizes via `unzip -l`).

| Option | Latest stable | `net8.0` package deps | Assemblies added to the tool | Bytes |
| --- | --- | --- | --- | --- |
| `System.CommandLine` 2.0.11 | 2026-08-11 | **none** | `System.CommandLine.dll` | 151,352 |
| `Spectre.Console.Cli` 0.55.0 | 2026-04-03 | `Spectre.Console` | `Spectre.Console.Cli.dll` + `Spectre.Console.dll` + `Spectre.Console.Ansi.dll` | 1,512,448 |

The `System.CommandLine` `.nuspec` has a literally empty dependency group for `net8.0`:

```xml
<group targetFramework="net8.0" />
<group targetFramework=".NETStandard2.0">
  <dependency id="System.Memory" version="4.5.5" exclude="Build,Analyzers" />
</group>
```

Caveat on the size figure: the package also carries 13 satellite resource assemblies
(~18.7 KB each, ~243 KB total) for localised messages. Those are packed into the tool
unless suppressed with `<SatelliteResourceLanguages>en</SatelliteResourceLanguages>`.
Spectre ships none, so on a like-for-like basis the gap narrows to roughly 400 KB vs
1.5 MB — still about 4x, and Spectre pulls three assemblies where System.CommandLine
pulls one.

### 1.5 `Spectre.Console.Cli`

By its own versioning it has **never declared itself stable**. Full version history from
`https://api.nuget.org/v3-flatcontainer/spectre.console.cli/index.json`: several hundred
`0.4x`–`0.5x` releases, latest stable **0.55.0**, and an in-flight `1.0.0-alpha.0.5` …
`1.0.0-alpha.0.16` line. Under SemVer, `0.y.z` explicitly disclaims a stable public API.

There is also a lag worth noting: `Spectre.Console.Cli` 0.55.0 (2026-04-03) depends on
`Spectre.Console >= 0.55.0`, while `Spectre.Console` itself has since moved to 0.56.0
(2026-06-05), 0.57.0, 0.57.1 and 0.57.2 (2026-07-02). The CLI half is two minor versions
behind the library half, and a 1.0 is in alpha — i.e. a breaking transition is pending,
not past.

This is decisive on the ticket's stated deciding factor. Spectre's strength is rendering
(tables, colour, progress), which Reach barely needs: its terminal output is a filter
string and a summary, and its canonical output is a JSON report.

### 1.6 Hand-rolled parser

Not recommended. It removes a 151 KB dependency-free assembly maintained by the same team
that ships the `dotnet` CLI, and buys back the obligation to implement POSIX/Windows
argument conventions, `--` passthrough, response files, help generation and completions.
The dependency being displaced is not large enough to justify the surface area. Worth
revisiting only if AOT/trim size becomes a hard requirement — and `System.CommandLine` is
documented as trim-friendly and AOT-capable, so even that motive is weak.

---

## 2. What `PackAsTool` actually requires

Read from the SDK on disk:
`C:\Program Files\dotnet\sdk\10.0.303\Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.PackTool.targets`.

Minimum project additions (per
`https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create`):

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>dotnet-reach</ToolCommandName>
<PackageOutputPath>./nupkg</PackageOutputPath>
```

`ToolCommandName` defaults to `$(TargetName)` if omitted (`PackTool.targets:224`). Because
the command begins with `dotnet-`, it is invocable both as `dotnet-reach` and as
`dotnet reach` — the docs state the rule explicitly: "If the command begins with the
prefix `dotnet-`, an alternative way to invoke the tool is to use the `dotnet` command and
omit the tool command prefix." That is precisely the PRD appendix's naming intent, and it
comes for free from the name.

`_PackToolValidation` (`PackTool.targets:274-295`) enforces exactly three constraints:

| Condition | Error resource |
| --- | --- |
| `TargetFrameworkIdentifier != '.NETCoreApp'` | `DotnetToolOnlySupportNetcoreapp` |
| TFM version `< 2.1` | `DotnetToolDoesNotSupportTFMLowerThanNetcoreapp21` |
| `TargetPlatformIdentifier != ''` on `net5.0`+ | `PackAsToolCannotSupportTargetPlatformIdentifier` |

So: `.NETCoreApp` only, at least 2.1, and **no platform-specific TFM** — `net8.0` is fine,
`net8.0-windows` is not. `net8.0` satisfies all three.

Two behaviours worth knowing:

- **`SuppressDependenciesWhenPacking` is forced to `true`** (`PackTool.targets:134`). A
  tool package declares *no* NuGet dependencies; the whole publish output is embedded in
  the package. Consumers never resolve a transitive graph. This is why "dependency
  footprint" for a tool means package bytes and assemblies loaded, not an install graph
  the user has to reason about — it reframes §1.4 as a download-size question.
- **The package layout is `tools/<tfm>/<rid|any>/`** (`PackTool.targets:206-218`), and
  `$(PublishRuntimeConfigFilePath)` is explicitly included in the packed files
  (`PackTool.targets:176`). That last line is the mechanism §3 depends on.

Multi-targeting works — verified against real packages: `csharpier` 1.3.0 ships
`tools/net8.0/any/`, `tools/net9.0/any/` and `tools/net10.0/any/`, each with its own
`DotnetToolSettings.xml` and `runtimeconfig.json`. `dotnet tool install --framework`
selects among them; the docs say "By default, the .NET SDK tries to choose the most
appropriate target framework."

The generated `DotnetToolSettings.xml` looks like this (from `gitversion.tool` 6.8.2):

```xml
<DotNetCliTool Version="1">
  <Commands>
    <Command Name="dotnet-gitversion" EntryPoint="gitversion.dll" Runner="dotnet" />
  </Commands>
</DotNetCliTool>
```

`Runner="dotnet"` matters: the tool is launched through the `dotnet` muxer against a
managed DLL, so the app's own `runtimeconfig.json` governs framework resolution.

---

## 3. `RollForward` — does a `net8.0` tool really run on a .NET-10-only machine?

**Yes, with `RollForward` set to `Major` or `LatestMajor`. Without it, no — it fails.**

### 3.1 The default fails, and the docs say so

From `https://learn.microsoft.com/en-us/dotnet/core/tools/troubleshoot-usage-issues`,
under "Runtime not found":

> Roll-forward won't occur by default in two common scenarios:
> - Only lower versions of the runtime are available. Roll-forward only selects later versions of the runtime.
> - **Only higher major versions of the runtime are available. Roll-forward doesn't cross major version boundaries.**
>
> If an application can't find an appropriate runtime, it fails to run and reports an error.

The default policy is `Minor`
(`https://learn.microsoft.com/en-us/dotnet/core/versions/selection`), which rolls to a
higher *minor* within the same major only. So an unconfigured `net8.0` tool on an agent
with only .NET 10 is a hard failure. This is exactly the risk the PRD's `LatestMajor`
decision is guarding against, and it is real.

### 3.2 How it is configured for a tool

Ordinary MSBuild property on the tool project:

```xml
<PropertyGroup>
  <RollForward>LatestMajor</RollForward>
</PropertyGroup>
```

The chain is traceable in the SDK on disk:

1. `Microsoft.NET.Sdk.targets:412` passes `RollForward="$(RollForward)"` to the
   `GenerateRuntimeConfigurationFiles` task, which writes `"rollForward"` into
   `*.runtimeconfig.json`.
2. `Microsoft.NET.PackTool.targets:176` includes `$(PublishRuntimeConfigFilePath)` in the
   packed tool files.
3. The file lands at `tools/<tfm>/any/<app>.runtimeconfig.json` inside the `.nupkg`.

Confirmed against shipping packages rather than inferred. `gitversion.tool` 6.8.2,
`tools/net8.0/any/gitversion.runtimeconfig.json`, verbatim as published:

```json
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "rollForward": "LatestMajor",
    "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" },
    "configProperties": { ... }
  }
}
```

(`csharpier` and `dotnet-outdated-tool` do the same with `"Major"`.)

### 3.3 Proof that the policy is what decides it

Experiment: take `gitversion.tool`'s shipped `net8.0` tool directory, rewrite only the
`runtimeconfig.json` to request `Microsoft.NETCore.App 7.0.0` — a major that is **not
installed on this machine**, while 8.0.30, 9.0.19 and 10.0.11 are — and vary the policy.
This reproduces "the tool targets an older major than anything present". Framework choice
observed with `DOTNET_HOST_TRACE=1`.

| `rollForward` | Host trace | Outcome |
| --- | --- | --- |
| `Minor` *(the default)* | `version_compatibility_range=minor` → `It was not possible to find any compatible framework version` | **Fails to start.** "You must install or update .NET to run this application. Framework: 'Microsoft.NETCore.App', version '7.0.0'" |
| `LatestPatch` | — | **Fails to start**, same error |
| `Major` | `version_compatibility_range=major, roll_to_highest_version=0` | Starts on **8.0.30** — the *lowest* higher major |
| `LatestMajor` | `version_compatibility_range=major, roll_to_highest_version=1` | Starts on **10.0.11** — the *highest* installed major |

This is the whole answer in one table, and it also surfaces the `Major` vs `LatestMajor`
distinction, which is easy to conflate:

- `Major` prefers the *oldest* runtime that will do. A `net8.0` tool lands on .NET 8 if
  present, and only reaches .NET 10 when nothing older exists. Closest to the tested
  configuration.
- `LatestMajor` always takes the *newest* runtime present. It maximises "installs
  anywhere", at the cost that Reach silently changes runtime the moment an agent gains a
  new .NET major — the least-tested combination, with no change to Reach.

The PRD chose `LatestMajor`. That remains defensible for a tool whose selling point is
installing on an unfamiliar agent, and it is what Microsoft's own sample tool does
(§3.4). Flagging the trade-off, not disputing the decision.

### 3.4 End-to-end proof, including install

`dotnetsay` 3.0.3 — Microsoft's own sample tool — is a **`net8.0`-only** package
(`tools/net8.0/` is its sole TFM folder) and ships with `"rollForward": "LatestMajor"`.
That is precisely Reach's proposed configuration, published by Microsoft.

Installed with the .NET 10.0.303 SDK into a scratch path:

```
$ dotnet tool install dotnetsay --tool-path <scratch>
You can invoke the tool using the following command: dotnetsay
Tool 'dotnetsay' (version '3.0.3') was successfully installed.
```

The SDK laid down the `net8.0` asset
(`.store/dotnetsay/3.0.3/dotnetsay/3.0.3/tools/net8.0/any/`) without complaint. Running
the installed shim under `DOTNET_HOST_TRACE=1`:

```
Attempting FX roll forward starting from version='[8.0.0]', … version_compatibility_range=major, roll_to_highest_version=1, prefer_release=1
Chose FX version [C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.11]
```

A `net8.0` tool, installed by a .NET 10 SDK, executing on the .NET 10 runtime. Install and
run are separate concerns and both hold.

**Scope limit, stated plainly:** this machine has .NET 8 installed as well, so I did not
literally observe a machine where 10 is the *only* runtime. What I did observe is that
`LatestMajor` makes the host skip the exactly-matching 8.0.x and select 10.0.11 anyway,
and that with 7.0.0 requested (genuinely absent) the host searches "the highest release
greater than or equal to" the reference across installed frameworks. Both make the
only-.NET-10 case a strict subset. I consider it established, but by construction rather
than by direct observation.

### 3.5 Two caveats

- **Preview runtimes are excluded.** The host trace shows `prefer_release=1`, and the docs
  state "Roll forward doesn't occur between preview versions of the runtime or between
  preview versions and release versions." A `net8.0` tool on an agent whose only runtime
  is a .NET 11 *preview* will still fail unless `DOTNET_ROLL_FORWARD_TO_PRERELEASE=1` is
  set. Relevant for early-adopter agents; not for mainstream CI.
- **There is a consumer-side escape hatch, so this is recoverable.** `dotnet tool install`
  and `dotnet tool exec` both take `--allow-roll-forward` (".NET 9.0 SDK and later" —
  "Allow tool to use a newer version of the .NET runtime if the runtime it targets isn't
  installed"). Per `https://github.com/dotnet/sdk/pull/37231` it writes `rollForward:
  Major` into the installed tool's runtime config (global), stores the state in the
  manifest (local), or rewrites the invocation as `dotnet --roll-forward Major …`
  (`dotnet tool run`). Note it uses `Major`, not `LatestMajor`, deliberately, to limit
  exposure to newer runtimes. Reach should still set the property itself — the flag
  requires the *user* to know about it, which is exactly the friction PRD §1.2 forbids.

---

## 4. Global vs local vs tool-path vs `tool exec` — which should CI use?

Definitions from `https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools` and
`https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec`:

| Form | Install | Version pinning | Notes for CI |
| --- | --- | --- | --- |
| Global (`-g`) | `%USERPROFILE%\.dotnet\tools`, auto-added to PATH | none in-repo | "Tool access is user-specific, not machine global." Version lives outside the repo — an unpinned global install makes builds non-reproducible |
| Tool-path (`--tool-path`) | Caller-chosen directory | none in-repo | PATH is **not** updated automatically; caller must use the full path. Useful for a container layer or a cached CI directory |
| Local (manifest) | NuGet global packages dir; `.config/dotnet-tools.json` committed | **version pinned in the repo** | "a contributor can clone the repository and invoke a single .NET CLI command to install all of the tools" — `dotnet tool restore`. Invoked as `dotnet reach` |
| `dotnet tool exec` / `dnx` | Nothing installed; NuGet cache only | `@version` or manifest | .NET 10.0.100 SDK and later |

The `dotnet tool exec` documentation gives a direct recommendation, which is worth quoting
because it settles the question rather than leaving it to taste:

> The `dotnet tool install -g` command does still serve an important purpose for users who
> want to permanently install a tool. However, **for users who want to try out a tool or
> run it in a CI/CD pipeline, `dotnet tool exec` is often a better fit.**

For PRD §1.2's "introduced in an afternoon, on a repository they have never seen":

- **Headline instruction: `dnx dotnet-reach@<version> -- …`** (or
  `dotnet tool exec dotnet-reach@<version>`). One line, no manifest, no PATH surgery, no
  state left behind, version pinned inline. Best possible answer to "add Reach to an
  unfamiliar pipeline", and it is the officially recommended CI path.
  **Constraint to document: requires the .NET 10.0.100 SDK or later.**
- **Documented alternative for teams that want the version in the repo:**
  `dotnet new tool-manifest` + `dotnet tool install dotnet-reach` + `dotnet tool restore`,
  then `dotnet reach`. Works on SDK 3.0+, pins the version in
  `.config/dotnet-tools.json`, and reviews like any other dependency. This is the right
  default for a team adopting Reach permanently.
- **Do not lead with `-g`.** Unpinned, user-scoped, outside the repo, and it makes the
  build depend on machine state.

Both paths need the tool published to a feed. Whether M1 publishes anywhere is listed as
open in [map.md](../map.md) ("CI for the Reach repository itself") and this research does
not settle it. Note `--tool-path` works fine against a local `.nupkg` directory via
`--add-source`, which covers pre-publication dogfooding.

Security note the docs are emphatic about, worth carrying into Reach's own documentation
since Reach will be asking strangers to install it: ".NET tools run in full trust. Don't
install a .NET tool unless you trust the author." And for the manifest path: "If the
manifest is modified by an untrusted party, it could cause the CLI to run malicious code."

---

## 5. Does tool packaging constrain the assemblies Reach can read?

### 5.1 The ticket's assumption is correct

**No.** The assemblies Reach reads are runtime *input data*, unrelated to the tool
package's own TFM.

- `System.Reflection.Metadata.MetadataReader` "Reads metadata as defined by the ECMA 335
  CLI specification" and "operates low-level constructs such as type and method
  definitions". It is opened over a `PEReader` on a file; it does **not** load the
  assembly into the runtime, so there is no compatibility relationship with the host
  process's framework. Any ECMA-335 image is readable — `net48`, `netstandard2.0`,
  `net10.0`, any target framework in a multi-targeted project's output.
  (`https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.metadatareader`)
- `MetadataLoadContext` is available for a higher-level reflection API, also without
  loading into the execution context.
- Portable PDB reading, which ADR-0003's checksum verification and the debug-symbol-based
  first-party assembly identification both depend on, uses the same reader
  (`MetadataReader.Documents`, `MethodDebugInformation`, `DebugMetadataHeader`).

There is even an upside from roll-forward. `System.Reflection.Metadata.dll` and
`System.Collections.Immutable.dll` are both in the `Microsoft.NETCore.App` shared
framework (verified present in both `8.0.30` and `10.0.11` on disk). A framework-dependent
`net8.0` tool compiles against the 8.0 reference surface but binds to the shared
framework's implementation at run time — so under `LatestMajor` Reach gets the **.NET 10**
metadata reader implementation while only using 8.0-era APIs. Rolling forward makes it
*better* at reading newly-emitted metadata, not worse.

One caution the docs raise, relevant because Reach reads whatever is in a customer's
output directory:

> This type is not designed to handle untrusted input. Malformed or malicious metadata can
> cause unexpected behavior, including out-of-bounds memory access, crashes, or hangs.

Reach reads assemblies the customer's own build just produced, so this is acceptable — but
a corrupt or truncated assembly is a crash risk, not a clean error, and the assembly
discovery step should be prepared for `BadImageFormatException` and worse.

### 5.2 A different `net8.0` constraint that did turn up

Not what the ticket asked, but it lands squarely on the same decision and would be worse
to discover during implementation. **Roslyn has dropped `net8.0`.** `lib/` folders in
`Microsoft.CodeAnalysis.CSharp`:

| Version | Published | `lib/` TFMs |
| --- | --- | --- |
| 4.14.0 | 2025-05-15 | `net8.0`, `net9.0`, `netstandard2.0` |
| 5.0.0 | 2025-11-18 | `net8.0`, `net9.0`, `netstandard2.0` |
| 5.3.0 | 2026-03-10 | `net10.0`, `net8.0`, `net9.0`, `netstandard2.0` |
| 5.6.0 | 2026-07-02 | `net10.0`, `net8.0`, `netstandard2.0` |
| **5.9.0** | **2026-08-17** | **`net10.0`, `netstandard2.0`** |

5.9.0 is built from Roslyn's `release/insiders` branch (per its `.nuspec` repository
element), i.e. the .NET 11 line. The last stable with a `net8.0` asset is **5.6.0**, which
is also what the installed SDK ships (`Roslyn\bincore` reports
`5.6.0-2.26377.103`).

Why it matters: [map.md](../map.md) commits to Roslyn for change detection and for
in-memory compilation in tests. A `net8.0` tool referencing Roslyn 5.9.0+ resolves the
**`netstandard2.0`** asset, which drags in a much larger dependency group —
`System.Buffers`, `System.Collections.Immutable` 10.0.1, `System.Memory`,
`System.Numerics.Vectors`, `System.Reflection.Metadata` 10.0.1,
`System.Runtime.CompilerServices.Unsafe`, `System.Text.Encoding.CodePages`,
`System.Threading.Tasks.Extensions` — versus the `net8.0` group in 5.6.0, which needs only
`System.Collections.Immutable` and `System.Reflection.Metadata`. Because
`SuppressDependenciesWhenPacking` inlines everything (§2), those all get packed into the
tool, and the tool then ships its own `System.Reflection.Metadata` 10.0.1 and
`System.Collections.Immutable` 10.0.1 alongside the shared framework's copies — a
classic source of binding friction, and it partly undoes §5.1's "you get the newer
in-box reader for free".

Three options, in rough order of preference. **This is a decision for the spec, not
something research can settle** — the trade-off is Reach's reach versus its dependency
hygiene:

1. **Stay on `net8.0` and pin Roslyn to 5.6.0.** Preserves the PRD's install-anywhere
   property, keeps the clean `net8.0` asset. Cost: a pinned compiler that ages, and the
   pin needs a comment explaining *why*, or someone will bump it.
2. **Multi-target `net8.0;net10.0`.** Proven to work for tool packages (§2 — `csharpier`
   ships three TFMs). Best of both, at the cost of a second build/test matrix leg. Note
   §5.1's identity concern: [CONTEXT.md](../../CONTEXT.md) makes the target framework part
   of **method identity**, so multi-targeting *Reach itself* is unrelated to that rule —
   it concerns the assemblies under analysis, not the tool. No conflict.
3. **Target `net10.0`.** Simplest dependency story; abandons `RollForward: LatestMajor`'s
   whole purpose and PRD §1.2's "installs on any modern agent".

---

## What I could not establish

- **No written post-GA API-stability commitment for `System.CommandLine` 2.0.** The
  stability conclusion rests on shipping behaviour (§1.1–1.3), not on a stated promise
  (§1.3).
- **No direct observation on a machine with only .NET 10.** Established by construction
  from host-resolution traces instead (§3.4).
- **Whether `dotnet tool install` performs any install-time runtime check.** The docs place
  "Runtime not found" under *"Installed .NET tool fails to run"*, and the *"installation
  fails"* section lists no runtime-version cause; a `net8.0`-only package installed
  cleanly under the .NET 10 SDK here. I did not read the SDK's installer source to confirm
  there is no check at all. The observed behaviour is what matters and it is favourable.
- **Whether 2.0.x will keep being serviced after 3.0 GA (2026-11-10).** 2.0.11 and
  3.0.0-preview.7 shipped the same day, and 2.0 is the .NET 10 LTS companion, so continued
  servicing is the reasonable expectation — but it is an expectation, not a documented
  policy I could find.

---

## Sources

Packaging and tools
- <https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools-how-to-create>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-exec>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/troubleshoot-usage-issues>
- <https://github.com/dotnet/sdk/pull/37231>
- `C:\Program Files\dotnet\sdk\10.0.303\Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.PackTool.targets`
- `C:\Program Files\dotnet\sdk\10.0.303\Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.Sdk.targets`

Roll-forward
- <https://learn.microsoft.com/en-us/dotnet/core/versions/selection>
- <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables>
- <https://github.com/dotnet/runtime/blob/main/docs/design/features/framework-version-resolution.md>

`System.CommandLine`
- <https://api.nuget.org/v3-flatcontainer/system.commandline/index.json>
- <https://api.nuget.org/v3/registration5-semver1/system.commandline/index.json>
- <https://github.com/dotnet/command-line-api/releases> (via `api.github.com`)
- <https://api.github.com/repos/dotnet/command-line-api/milestones>
- <https://github.com/dotnet/command-line-api/issues/2576>
- <https://learn.microsoft.com/en-us/dotnet/standard/commandline/>
- <https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.0/10.0.0.md>

Spectre and Roslyn
- <https://api.nuget.org/v3-flatcontainer/spectre.console.cli/index.json>
- <https://www.nuget.org/packages/Spectre.Console.Cli>
- <https://www.nuget.org/packages/Spectre.Console>
- <https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.csharp/index.json>

Metadata reading
- <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.metadatareader>

Packages inspected directly (downloaded from `api.nuget.org/v3-flatcontainer`):
`System.CommandLine` 2.0.11 · `Spectre.Console.Cli` 0.55.0 · `Spectre.Console` 0.57.2 ·
`Spectre.Console.Ansi` 0.57.2 · `Microsoft.CodeAnalysis.CSharp` 4.14.0/5.0.0/5.3.0/5.6.0/5.9.0 ·
`csharpier` 1.3.0 · `GitVersion.Tool` 6.8.2 · `dotnet-outdated-tool` 4.8.1 ·
`dotnetsay` 3.0.3 · `dotnet-reportgenerator-globaltool` 5.5.11 · `dotnet-sonarscanner` 11.3.0
