# What is `dotnet test --affected-tests`?

Research for [issue 16](../issues/16-what-is-dotnet-test-affected-tests.md). Investigated 2026-09-05
against primary sources only: `dotnet/sdk` source and PRs, the `dotnet/sdk` design issue,
`microsoft/testfx` source, RFC and rollout docs, the public .NET release index, and the NuGet API.
One class of claim is **measured**: strings scanned out of the .NET SDK 10.0.303 installed on this
machine.

Claims that could **not** be established are marked **UNRESOLVED** in §8 and say what was checked.
Product judgement is confined to §9 and is not mixed into the facts.

---

## 0. Headline findings

1. **It is coverage-based.** The design issue states the workflow verbatim: *"Execute tests and
   record which source files and binaries are exercised by each test."* The testfx RFC describes the
   collector as performing *"managed x64 profiler batching"* and owning a *"multi-run/instrumentation
   plan"*. This is runtime instrumentation, not static analysis. §2 — **this is the decisive
   finding.**
2. **It needs exactly the standing infrastructure PRD §9.1 rejected**, and Microsoft's own dogfood
   pipeline shows it concretely: a trusted main-branch collection run, an Azure Pipelines `Cache@2`
   store keyed by OS/arch/config, a 7-day expiry, and a full-suite fallback on cache miss. §3.
3. **The SDK contains none of the logic.** `dotnet/sdk` owns only the two flags, some validation,
   and one environment variable it sets on the child test process. Everything real lives in *"a
   separately distributed MTP extension"* — which **does not exist publicly**. §1, §4.
4. **🔴 Correction to [research 01 §11](01-filter-dialects-and-runner-detection.md): these options
   are NOT in a shipping SDK.** They are absent from every public .NET release. The newest public
   .NET 11 build is **preview.7** (2026-08-11), and the `release/11.0.1xx-preview7` branch does not
   contain them. They exist only in `main`, `release/11.0.1xx` and `release/11.0.1xx-rc1` — i.e.
   unreleased RC1 dailies. **Measured:** zero occurrences in SDK 10.0.303 on disk. §5.
5. **Even Microsoft cannot turn it on.** `microsoft/testfx` prepared its pipeline for the feature and
   then deliberately disabled it, because *"the affected-test extension package and its public
   local-filesystem storage contract are also not available yet."* §4.
6. **Zero documentation.** No Learn page; **0 hits** for either option name in `dotnet/docs`. The
   option description strings in the SDK's `.resx` are the only prose that exists. §1.2.
7. **MTP-only, by explicit design decision.** *"VSTest support is out of scope for the initial
   implementation."* Initial slice is *"managed MTP test applications"*. §6.
8. **The selection is delivered as MTP test-node UIDs**, not as a `--filter` string — via
   `--filter-uid` (child launch) or the new `ITestExecutionFilterProvider` API. §6.2. This is
   directly relevant to research 01's filter-length and dialect findings, and is the one part of
   this work Reach could plausibly reuse. §9.4.
9. **No follow-up activity in a month.** One SDK PR (merged 2026-08-05), one testfx rollout PR
   (merged 2026-08-05), nothing since, as of 2026-09-05. §5.3.

---

## 1. What the two options are, from `dotnet/sdk` source

Verified against `dotnet/sdk` `main`. All quoted code is from files downloaded from
`raw.githubusercontent.com` at the commit of `main` on 2026-09-05.

### 1.1 The option definitions and the gate

`src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/Test/TestCommandDefinition.MicrosoftTestingPlatform.cs`:

```csharp
public const string EnableAffectedTestsEnvironmentVariable = "DOTNET_CLI_ENABLE_AFFECTED_TESTS";
public const string CollectTestMapOptionName = "--collect-test-map";
public const string AffectedTestsOptionName  = "--affected-tests";

AffectedTestsEnabled = EnvironmentVariableParser.ParseBool(
    Environment.GetEnvironmentVariable(EnableAffectedTestsEnvironmentVariable), defaultValue: false);

CollectTestMapOption = new(CollectTestMapOptionName)
{ Description = CommandDefinitionStrings.CmdCollectTestMapDescription, Arity = ArgumentArity.Zero, Hidden = !AffectedTestsEnabled };

AffectedTestsOption = new(AffectedTestsOptionName)
{ Description = CommandDefinitionStrings.CmdAffectedTestsDescription,  Arity = ArgumentArity.Zero, Hidden = !AffectedTestsEnabled };
```

Both are `ArgumentArity.Zero` — bare boolean flags, no value. They are registered unconditionally and
merely *hidden* from help unless the gate is set; a validator then rejects them at use time.

Source: <https://github.com/dotnet/sdk/blob/main/src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/Test/TestCommandDefinition.MicrosoftTestingPlatform.cs>

### 1.2 The description strings — the only prose that exists anywhere

`src/Cli/Microsoft.DotNet.Cli.Definitions/CommandDefinitionStrings.resx` (lines 457–474):

| Resource | Value |
| --- | --- |
| `CmdCollectTestMapDescription` | **"Run tests and write the source-to-test map used by affected-test selection."** |
| `CmdAffectedTestsDescription` | **"Run only tests linked in the test map to sources changed in Git."** |
| `CmdAffectedTestsFeatureDisabled` | "Affected-test selection is experimental. Set the '{0}' environment variable to '1' to enable it." |
| `CmdAffectedTestsOptionsMutuallyExclusive` | "The options '--collect-test-map' and '--affected-tests' cannot be used together." |
| `CmdCollectTestMapCannotRunModulesInParallel` | "The option '--collect-test-map' cannot be combined with '--max-parallel-test-modules'. **Test maps are collected one module at a time.**" |
| `CmdCollectTestMapCannotRequireMinimumTests` | "The option '--collect-test-map' cannot be combined with '--minimum-expected-tests'. **Collection batches do not report test totals to the parent process.**" |

Also in `src/Cli/dotnet/Commands/CliCommandStrings.resx`:

| Resource | Value |
| --- | --- |
| `CmdListDevicesAndAffectedTestsMutuallyExclusive` | "The '--list-devices' option cannot be combined with '--collect-test-map' or '--affected-tests'." |
| `CmdAffectedTestsResponseFilesMustBeConsistent` | "Forwarded response files must select the same affected-test operation for every test application. Specify '--collect-test-map' or '--affected-tests' directly on 'dotnet test' instead." |

Sources: <https://github.com/dotnet/sdk/blob/main/src/Cli/Microsoft.DotNet.Cli.Definitions/CommandDefinitionStrings.resx>,
<https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Commands/CliCommandStrings.resx>

Two things are stated as fact by these strings and matter later: **"source-to-test map"** (not a call
graph) and **"sources changed in Git"** (the same change-detection input Reach uses).

### 1.3 Everything the SDK actually does at runtime

This is the whole of it. `src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs` sets one environment
variable on the child test process (lines 325–336):

```csharp
if (TestOptions.CollectTestMap)
    processStartInfo.Environment[TestOptions.AffectedTestsModeEnvironmentVariable] = TestOptions.CollectTestMapMode;
else if (TestOptions.AffectedTests)
    processStartInfo.Environment[TestOptions.AffectedTestsModeEnvironmentVariable] = TestOptions.RunAffectedTestsMode;
else
    processStartInfo.Environment.Remove(TestOptions.AffectedTestsModeEnvironmentVariable);
```

and appends the flag to the child's command line (lines 375–383). The constants are in
`src/Cli/dotnet/Commands/Test/MTP/Options.cs` (lines 33–41):

```csharp
internal const string AffectedTestsModeEnvironmentVariable = "DOTNET_CLI_TEST_AFFECTED_TESTS_MODE";
internal const string CollectTestMapMode = "collect";
internal const string RunAffectedTestsMode = "run";
...
public bool IsAffectedTestsMode => CollectTestMap || AffectedTests;
```

There is **no** map reading, map writing, git diffing, instrumentation or selection logic anywhere in
`dotnet/sdk`. The PR that added the feature says so explicitly:

> "The SDK owns only the command-line and multi-module orchestration boundary. **Repository analysis,
> test-map collection/storage, and affected-test filtering remain in a separately distributed MTP
> extension.**"

Source: <https://github.com/dotnet/sdk/pull/55574> (merged 2026-08-05, author `Evangelink`)

### 1.4 Validation and behavioural consequences

`src/Cli/dotnet/Commands/Test/MTP/MicrosoftTestingPlatformTestCommand.cs`:

- `ValidateAffectedTestsOptions` (lines 319–349): throws if the gate is unset; the two options are
  mutually exclusive; `--collect-test-map` is incompatible with `--max-parallel-test-modules` and
  with `--minimum-expected-tests`.
- `GetDegreeOfParallelism` (lines 729–742) — **collection is forced fully serial**:
  ```csharp
  private static int GetDegreeOfParallelism(ParseResult parseResult, bool collectTestMap)
  {
      if (collectTestMap) return 1;
      ...
  }
  ```
- Zero-test verdict (line 595):
  ```csharp
  internal static bool ShouldFailForNoExecutedTests(bool isAffectedTestsMode, int totalTests, int skippedTests)
      => (!isAffectedTestsMode && totalTests == 0) ||
          (totalTests > 0 && totalTests == skippedTests);
  ```
  So **an affected-tests run that selects nothing succeeds**, but a run whose every selected test was
  skipped still fails. (This confirms and refines research 01 §11.)
- `DetectAffectedTestsOptionsInForwardedResponseFiles` walks recursive response files per test
  application, resolved against each application's effective working directory, so the mode cannot be
  activated inconsistently across modules.

Source: <https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/Commands/Test/MTP/MicrosoftTestingPlatformTestCommand.cs>

The PR body adds the reason for the SDK-to-extension marker:

> "Marks SDK-launched children with `DOTNET_CLI_TEST_AFFECTED_TESTS_MODE` so the companion extension
> can reject activation injected through launch profiles, project run arguments, or test
> configuration."

---

## 2. 🔴 The technique: coverage-based runtime instrumentation

This is the decisive question in the ticket. Three independent primary sources agree, and none of the
evidence is inferred from a name.

### 2.1 The design issue states the workflow verbatim

`dotnet/sdk` issue **#55405**, *"Add affected-test selection to `dotnet test`"*, opened 2026-07-22 by
`Evangelink`, label `Area-dotnet test (MTP)`, still **open**:

> "An internal proof of concept has demonstrated this workflow:
>
> 1. **Execute tests and record which source files and binaries are exercised by each test.**
> 2. **Persist that mapping as a reusable source of truth.**
> 3. Compare the current Git changes with the stored mapping.
> 4. Discover the current tests and run only the affected subset."

That is PRD §3's definition of **coverage-based analysis** almost word for word: *"recording, during a
previous test run, which code each individual test executed, and consulting that record."*

The same issue makes instrumentation an explicit configuration concern:

> "The configuration needs to express: … **product modules to include or exclude from
> instrumentation**; … execution tuning, diagnostics, result reporting, coverage, and **native
> instrumentation** where supported."

and lists as follow-up scope:

> "managed and native instrumentation; code coverage and TRX generation…"

and gives the reason the engine is kept out of `dotnet.dll`:

> "the proof of concept brings storage clients, Git/MSBuild/test-platform dependencies, **code-coverage
> components, and native instrumentation payloads**. This would increase SDK size and
> dependency/version risk…"

Source: <https://github.com/dotnet/sdk/issues/55405>

### 2.2 The testfx RFC names the collector a profiler

`microsoft/testfx` `docs/RFCs/020-Test-Execution-Filter-Providers.md`, section *"Concrete migration:
affected-test selection"* (lines 445–447):

> "The extension's refresh operation remains an orchestrator. It performs **managed x64 profiler
> batching** and owns a **multi-run/instrumentation plan**, so converting it to a filter provider
> would violate the constraint-versus-planning boundary in this RFC."

Source: <https://github.com/microsoft/testfx/blob/main/docs/RFCs/020-Test-Execution-Filter-Providers.md>

### 2.3 The configuration schema has an instrumentation scope

`microsoft/testfx` `global.json`, `test.affectedTests`:

```json
"affectedTests": {
  "changes": {
    "ignore": [".github/**", "docs/**", "**/*.md"],
    "forceAllTests": [
      "global.json", "Directory.Build.*", "Directory.Packages.props", "NuGet.config",
      "TestFx.slnx", "*.slnf", "Build.cmd", "Test.cmd", "build.sh", "test.sh",
      "eng/**", "src/**/Directory.Build.*", "test/**/Directory.Build.*"
    ]
  },
  "instrumentation": {
    "include": ["src/**"],
    "exclude": ["artifacts/**", "samples/**", "test/**", "**/bin/**", "**/obj/**"]
  }
}
```

Source: <https://github.com/microsoft/testfx/blob/main/global.json>

`eng/validate-affected-tests.ps1` (lines 16–40) enforces that all four arrays are non-empty and that
`changes.forceAllTests` includes `Directory.Build.*`, `src/**/Directory.Build.*` and
`test/**/Directory.Build.*` — i.e. build-infrastructure changes escape the map entirely and force the
full suite.
Source: <https://github.com/microsoft/testfx/blob/main/eng/validate-affected-tests.ps1>

### 2.4 Conclusion

**`--collect-test-map` is per-test coverage collection under managed (and, in scope, native)
instrumentation. It is not project-graph reachability and not static analysis of any kind.**
`--affected-tests` consults the persisted map, intersected with a Git diff of the working tree
against the repository's change scopes.

The two techniques therefore have the opposite blind spots, exactly as PRD §9.1 anticipated: coverage
never has to widen through an interface but cannot see a test that has not yet run under
instrumentation; static reverse reachability sees new and never-run tests but must widen through
interfaces and needs framework models for reflection-mediated edges.

---

## 3. The state it requires, and where that state lives

### 3.1 What the design issue requires of storage

From #55405:

> "mapping storage and a context/tag that distinguishes configurations such as OS and architecture"

> "Mapping keys also need to account for **target framework, runtime/device, OS, architecture**, and
> other inputs that can change test behavior."

Initial vertical slice: *"filesystem mapping storage"*. Follow-up scope: *"Azure Blob Storage,
including local caching and standard Azure credential flows; Azure DevOps artifact storage."*

Also:

> "**The repository must provide affected-test configuration and a compatible implementation package.
> The CLI should not implicitly download tooling.**"

> "There are three contracts to version deliberately: SDK-to-worker control and event protocol;
> worker-to-MTP JSON-RPC compatibility; **persisted test-mapping format**."

### 3.2 What Microsoft's own rollout looks like in practice

`microsoft/testfx` `docs/affected-test-selection.md`, *"Storage design"*:

> "The map should use the extension's local-filesystem provider rooted at
> `$(Pipeline.Workspace)\affected-test-map`. Azure Pipelines `Cache@2` transfers that directory
> between runs without credentials:
>
> - trusted main builds can restore the previous map and publish a new immutable cache entry;
> - PR and fork-PR builds can read the target branch's cache scope but cannot write to it;
> - the cache prefix includes its manual compatibility version, OS, architecture, and configuration;
> - the unique build ID suffix lets every successful main collection publish a new map;
> - prefix restore selects the newest compatible map.
>
> **Azure Pipelines caches expire after seven days without activity. A cache miss is therefore an
> expected state, not a test failure: the PR lane runs the unchanged full test command.** The same
> fallback runs when the extension rejects a missing, stale, or incompatible map, and scheduled or
> manual builds always keep full validation."

And the roles:

> "The trusted main-branch Windows Release test is the future `--collect-test-map` entry point.
> The Windows Release PR test is the future `--affected-tests` entry point."

Source: <https://github.com/microsoft/testfx/blob/main/docs/affected-test-selection.md>

The pipeline template `eng/pipelines/steps/test-windows-configuration-tests.yml` makes it concrete
(collect branch lines 42–61, run branch lines 63–94, fallback lines 96–113):

```yaml
- task: Cache@2
  displayName: Restore and publish affected-test map
  inputs:
    key: '"affected-tests" | "${{ parameters.affectedTestsCacheVersion }}" | "$(Agent.OS)" | "$(Agent.OSArchitecture)" | "$(_BuildConfig)" | "$(Build.BuildId)"'
    restoreKeys: |
      "affected-tests" | "${{ parameters.affectedTestsCacheVersion }}" | "$(Agent.OS)" | "$(Agent.OSArchitecture)" | "$(_BuildConfig)"
    path: '$(Pipeline.Workspace)\affected-test-map'
    cacheHitVar: AffectedTestsMapCacheRestored
```

```yaml
- script: |
    dotnet test -c $(_BuildConfig) --no-build ... --collect-test-map
  env:
    DOTNET_CLI_ENABLE_AFFECTED_TESTS: 1
```

and the PR lane, which treats affected-test failure as *"run everything"* rather than an error:

```powershell
dotnet test -c $(_BuildConfig) --no-build ... --affected-tests
$exitCode = $LASTEXITCODE
if ($exitCode -eq 0) { ...AffectedTestsSucceeded=true; exit 0 }
if ($exitCode -in 2, 8) { ...error...; exit $exitCode }
Write-Host "##vso[task.logissue type=warning]Affected-test selection failed; running the full test suite."
exit 0
```

with a third step, *"Test (affected-test fallback)"*, conditioned on
`or(ne(Build.Reason,'PullRequest'), eq(AffectedTestsMapCacheRestored,'false'), ne(AffectedTestsSucceeded,'true'))`.

Also recorded there: *"Selected-test runs do not publish their partial coverage as the repository
coverage report. Collection and full fallback runs still publish complete coverage."*

Source: <https://github.com/microsoft/testfx/blob/main/eng/pipelines/steps/test-windows-configuration-tests.yml>

### 3.3 Conclusion on state

**The feature requires persisted cross-run state whose lifecycle a pipeline must own.** The concrete
shape Microsoft ships to itself is: a trusted collection lane on main running the whole suite serially
under instrumentation; a keyed, versioned artifact store; a cache-miss policy; and a full-suite
fallback leg that must be kept working permanently. That is item-for-item the standing infrastructure
PRD §9.1 declined, and it is directly incompatible with ADR-0001.

---

## 4. The extension: private, unpublished, and blocking Microsoft's own rollout

- The SDK PR: *"…remain in a separately distributed MTP extension."*
- The RFC calls it *"A **private** affected-test extension"*.
- `docs/affected-test-selection.md`: *"The rollout is intentionally disabled… **The affected-test
  extension package and its public local-filesystem storage contract are also not available yet.**
  Ordinary test commands therefore remain unchanged."*
- Same doc: *"The `storage` property is deliberately absent from `test.affectedTests` until the
  extension package publishes the exact local-filesystem provider schema. Adding an invented provider
  or path setting now would create configuration that cannot be validated."*
- `eng/validate-affected-tests.ps1` line 68 enforces that the rollout stays off:
  `if ($affectedTestsEnabled -and $null -eq $affectedTests.storage) { throw ... }` — and `global.json`
  has no `storage` key. **Measured:** as of 2026-09-05 `testfx`'s `global.json` still has no
  `storage`, so `enableAffectedTests` is necessarily `false`.

**NuGet check (2026-09-05).** No such package is published. `Microsoft.Testing.Extensions.AffectedTests`,
`.TestMap`, `.AffectedTest`, `.TestImpact` and `Microsoft.DotNet.AffectedTests` all return 404 from
`api.nuget.org/v3-flatcontainer/{id}/index.json`. A search of the NuGet query API for
`Microsoft.Testing.Extensions` returns Telemetry, TrxReport, VSTestBridge, CodeCoverage, Retry,
HangDump, CrashDump, AzureDevOpsReport, GitHubActionsReport, HotReload, Fakes — **and nothing
affected-test related**.

So today the two options are unreachable in practice: even with the gate set and an RC1 daily SDK,
there is no extension to answer them.

---

## 5. Gate, shipping vehicle and timeline

### 5.1 The two environment variables

| Variable | Values | Who sets it | Purpose |
| --- | --- | --- | --- |
| `DOTNET_CLI_ENABLE_AFFECTED_TESTS` | `1` | the user / pipeline | Un-hides and un-blocks both options. Parsed with `defaultValue: false`. |
| `DOTNET_CLI_TEST_AFFECTED_TESTS_MODE` | `collect` \| `run` | **the SDK only** | SDK-to-extension authorization marker on the child test process. |

`docs/affected-test-selection.md`: *"`DOTNET_CLI_TEST_AFFECTED_TESTS_MODE` is an SDK-to-extension
authorization marker. **Repository scripts and pipeline definitions must not set it.**"*
`validate-affected-tests.ps1` (lines 111–116) fails the build if the pipeline sets it.

### 5.2 Which SDKs contain the options — checked branch by branch

Fetched `TestCommandDefinition.MicrosoftTestingPlatform.cs` from each branch and tested for
`AffectedTestsOptionName`:

| Branch | Contains the options? |
| --- | --- |
| `main` | **yes** |
| `release/11.0.1xx` | **yes** |
| `release/11.0.1xx-rc1` | **yes** |
| `release/11.0.1xx-preview7` | no |
| `release/11.0.1xx-preview6` | no |
| `release/10.0.4xx` | no |
| `release/10.0.1xx` | file does not exist (404) |

**Public release status** (`https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json`,
read 2026-09-05):

| Channel | Latest release | Date | Latest SDK | Support phase |
| --- | --- | --- | --- | --- |
| 11.0 | `11.0.0-preview.7` | 2026-08-11 | `11.0.100-preview.7.26381.103` | preview |
| 10.0 | `10.0.11` | 2026-08-11 | `10.0.400` | active |

Since preview.7 is the newest public .NET 11 build and its branch does not contain the options,
**neither option is present in any publicly released .NET SDK.** They exist only in unreleased
`rc.1`-labelled dailies. `microsoft/testfx` pins `11.0.100-rc.2.26425.121` in `global.json`, an
internal daily.

**Measured, local:** I scanned every file under `C:\Program Files\dotnet\sdk\10.0.303`
(ASCII and UTF-16 decoded) for `collect-test-map`, `affected-tests`, `AFFECTED_TESTS`, `AffectedTests`
and `TestMap`: **zero hits**. The same scan for the known-present strings `--list-tests` and
`minimum-expected-tests` hit `dotnet.dll` and `Microsoft.DotNet.Cli.Definitions.dll`, confirming the
scan works.

This corrects research 01 §11, which described these as *"in the shipping .NET 11 SDK"*.

### 5.3 Timeline and activity

| Date | Event |
| --- | --- |
| 2026-07-22 | `dotnet/sdk#55405` design issue opened (`Evangelink`). Still **open**. |
| 2026-07-26 → 07-28 | `microsoft/testfx#10235` — RFC 020 + implementation of `ITestExecutionFilterProvider`. Merged. |
| 2026-08-03 → 08-05 | `dotnet/sdk#55574` — *"Add affected test selection to dotnet test"*. Merged into `main`. 43 files, +1932/−17. |
| 2026-08-05 | `microsoft/testfx#10450` — *"Prepare affected-test selection rollout"*. Merged, **deliberately disabled**. |
| 2026-08-06 | `dotnet/sdk#55595` (a routine VMR codeflow) merged; testfx notes RC1 dailies before it break `dotnet tool restore`. |
| 2026-09-05 | No further affected-test PR in either repo. Still gated, still undocumented, extension still unpublished. |

`.NET 11 GA` is not stated in any source I read; on the normal cadence it would be November 2026, but
**that is my inference, not an established fact** (§8).

### 5.4 Public design discussion

There is exactly one substantial public design document: **`dotnet/sdk#55405`**. It is long, explicit,
and is the single best source on intent. Notable positions taken there:

- On naming: the issue proposed `--refresh-test-mappings` / `--run-affected-tests`; what shipped into
  `main` is `--collect-test-map` / `--affected-tests`. The rename is not explained in any source I
  found.
- On safety: *"If mappings are missing or incompatible, `--run-affected-tests` should fail with
  actionable guidance rather than silently skipping tests. **Whether a configurable 'run all tests'
  safety fallback is desirable remains an open question.**"*
- On conservatism: *"Conservative selection remains the default: **changes outside the configured
  source scope should cause all tests for the relevant module to run** rather than risk missing
  regressions."* (The same instinct as PRD §8, at module granularity.)
- On architecture: the engine runs as an **out-of-process worker** driven over a versioned JSON-RPC
  contract, which itself drives MTP test hosts over MTP's JSON-RPC server mode
  (`testing/discoverTests`, `testing/runTests`).

RFC 020 itself is explicitly *not* about affected tests — *"This RFC does not define: … affected-test
terminology or source-to-test mapping"* — it only provides the composition primitive the extension
needs.

---

## 6. Runner and framework support

### 6.1 MTP only, deliberately

From #55405:

> "The feature is initially available only when `dotnet test` uses `Microsoft.Testing.Platform`.
> **VSTest support is out of scope for the initial implementation** and can be considered later based
> on demand and architectural fit."

> "Proposed initial vertical slice: **managed MTP test applications**; project and solution inputs,
> including multi-targeted projects; SDK-resolved test modules and launch context; **Git-based change
> detection**; **filesystem mapping storage**…"

> "The initial proposal deliberately targets MTP because its JSON-RPC server mode already supports
> discovery followed by execution of selected test-node IDs."

The options are registered only on `TestCommandDefinition.MicrosoftTestingPlatform`, so they do not
exist on the VSTest `dotnet test` surface at all.

### 6.2 How the selection reaches the test host

Per RFC 020's migration section, the extension has two paths:

1. **Compatibility path** — a `RunAffectedTestsOrchestrator` *"computes the selected UIDs, removes its
   parent activation option to prevent recursive activation, and launches the test host with the
   built-in `--filter-uid` option. Large selections are written to response files, recursive response
   files are supported, and the child execution remains connected to `dotnet test` reporting."*
2. **Provider path** — registers an `ITestExecutionFilterProvider` returning a
   `TestNodeUidListFilter` of the affected UIDs, which MTP ANDs with the user's request filter.

Both express the selection as **MTP test-node UIDs**, not as a `--filter` expression. RFC 020 notes the
consequence: *"A large direct selection stays in memory and therefore has no command-line-length
limit."*

### 6.3 Which frameworks can receive it

No primary source states the extension's supported-framework matrix (§8). What can be established
about the *platform* pieces:

| Framework | `TestNodeUidListFilter` (`--filter-uid`) | `CompositeTestExecutionFilter` (provider path) |
| --- | --- | --- |
| MSTest (native MTP) | yes | yes — RFC 020: *"Native MSTest and VSTestBridge must … traverse `CompositeTestExecutionFilter` recursively"* |
| NUnit (via `Microsoft.Testing.Extensions.VSTestBridge`) | yes, through the bridge | yes, through the bridge |
| xUnit v3 (own MTP integration, not the bridge) | **yes** — `TestPlatformTestFramework.cs` handles `NopFilter` and `TestNodeUidListFilter` (lines 177–179, 217–218) | **no evidence** — 0 hits for `CompositeTestExecutionFilter` in `xunit/xunit` |

Sources: <https://github.com/microsoft/testfx/blob/main/docs/RFCs/020-Test-Execution-Filter-Providers.md>,
<https://github.com/xunit/xunit/blob/main/src/common/MicrosoftTestingPlatform/TestPlatformTestFramework.cs>
(GitHub code search, `repo:xunit/xunit`, 2026-09-05)

RFC 020: *"Other test frameworks must add composite handling before opting into provider scenarios
that can produce more than one constraint."* An adapter that receives a representation it cannot
translate *"must fail with an actionable unsupported-filter diagnostic; silently treating it as
`NopFilter` is forbidden"* — i.e. the failure mode is a hard error, not a silent under-selection.

---

## 7. Answering the ticket's questions directly

| Question | Answer | Confidence |
| --- | --- | --- |
| What does `--collect-test-map` do? | Runs the tests under instrumentation and writes a persisted **source-to-test map** recording *"which source files and binaries are exercised by each test"*. Forced serial (one module at a time). | High — design issue + resx + SDK source |
| What does `--affected-tests` do? | Diffs the working tree against Git, intersects the changed paths with the stored map (subject to `changes.ignore` / `changes.forceAllTests`), and runs the resulting test-node UIDs. | High — resx + design issue + testfx config |
| Which technique? | **Per-test coverage via runtime instrumentation** (managed profiler now, native in scope). Not project-graph reachability, not static analysis. | **High** — three independent primary sources (§2) |
| What does a test map contain? | A mapping from **source files and binaries** to the tests that exercised them, keyed by TFM, runtime/device, OS and architecture. Exact schema is private. | Medium — stated in prose only; format unpublished |
| Where is it stored? | Filesystem provider (initially); in Microsoft's own pipeline, `$(Pipeline.Workspace)\affected-test-map` moved between runs by Azure Pipelines `Cache@2`. Azure Blob and AzDO artifacts are follow-up scope. | High |
| What state between runs? | **Persistent, cross-run, cross-build state**, produced by a trusted collection lane, consumed by PR lanes, versioned, expiring, with a mandatory full-suite fallback. | High |
| Gating variable? | `DOTNET_CLI_ENABLE_AFFECTED_TESTS=1`; plus the SDK-internal `DOTNET_CLI_TEST_AFFECTED_TESTS_MODE`. | High — source |
| Shipping vehicle / timeline? | `dotnet/sdk` `main` + `release/11.0.1xx` + `release/11.0.1xx-rc1`. **Not in any public release.** The engine is an unpublished private extension. No GA commitment found. | High |
| Public design discussion? | Yes — `dotnet/sdk#55405` (open), `dotnet/sdk#55574` (merged), `microsoft/testfx#10235` / RFC 020, `microsoft/testfx#10450` and `docs/affected-test-selection.md`. | High |
| MTP only? | Yes, by explicit decision; VSTest out of scope. | High |
| Which frameworks? | Not stated. Platform plumbing supports MSTest and (via VSTestBridge) NUnit fully; xUnit v3 supports the UID filter but shows no composite support. | Low–Medium |

---

## 8. What I could not establish (UNRESOLVED)

1. **The map's on-disk format, schema and size.** Explicitly withheld: *"the extension package [has
   not] published the exact local-filesystem provider schema."* So the question research 01 §11 posed
   — *"whether the test map is a format Reach could emit or consume"* — **cannot be answered today.**
   There is no published format.
2. **Selection granularity of the map.** Whether it maps source *files* to test *methods*, test
   *cases*, or test-node UIDs. #55405 says "source files and binaries" on one side and "tests" on the
   other; nothing pins the test-side granularity.
3. **Whether `--collect-test-map` requires a full instrumented suite run each time.** #55405 says
   `--refresh-test-mappings` would *"create or update"* the mappings, implying incremental refresh is
   intended, but no source describes the actual update algorithm. Microsoft's own pipeline runs it as
   a full-suite lane.
4. **Which profiler.** RFC 020 says *"managed x64 profiler"*. `Microsoft.Testing.Extensions.CodeCoverage`
   (18.11.0) exists on NuGet and is profiler-based, but **no primary source links it to the
   affected-test extension.** I did not establish the platform matrix either — "x64" appears in one
   clause of one design doc and is not a support statement.
5. **The extension's supported test frameworks**, package id, licence, price, acquisition channel and
   whether it will ever be public. Not stated anywhere.
6. **How "stale or incompatible" maps are detected.** `docs/affected-test-selection.md` says the
   extension *"rejects a missing, stale, or incompatible map"*; the rule is not published.
7. **Whether .NET 11 GA will ship these options, and whether they will stay env-var-gated.** No source
   commits to either. That .NET 11 GA lands around November 2026 is my inference from the historical
   release cadence, **not** an established fact.
8. **Why the options were renamed** from the proposed `--refresh-test-mappings` / `--run-affected-tests`
   to `--collect-test-map` / `--affected-tests`. No discussion found.
9. **The exit-code contract for affected-tests mode beyond zero-tests.** The testfx pipeline treats
   exit codes 2 and 8 as real failures and everything else as "fall back to the full suite", but I
   found no specification of what the extension returns when it declines to select.
10. **Whether the internal proof of concept exists as a shipped product elsewhere** (e.g. an internal
    Microsoft or Azure DevOps offering). #55405 references it only as "an internal proof of concept".

---

## 9. Product judgement

*Stated separately from the facts above, as the ticket requires. Sections 1–8 are the evidence; this
section is opinion built on it.*

### 9.1 The headline: this does not make M1 a poor investment

**It is complementary, and Reach should proceed.** Not because the overlap is small — the problem is
identical and the change input (a Git diff) is identical — but because the *technique* differs in the
one dimension the PRD already made the deciding one.

PRD §9.1 rejected coverage-based selection on a single ground: *"it requires standing infrastructure…
a main-branch build running the full suite under per-test instrumentation on every commit, storage
serving indexes by commit, and a policy for cache misses."* Microsoft's feature is that description
made literal. Their own dogfood pipeline is a collection lane on `main`, a `Cache@2` store keyed by
OS/arch/config with a manual compatibility version, a seven-day expiry, an explicit "cache miss is
expected" policy, and a permanent full-suite fallback leg that can never be deleted. §9.1's reasoning
was not merely correct — it now has a worked example authored by the team building the alternative.

So the strategic finding is the opposite of "Reach is redundant": **the .NET team independently
converged on the design PRD §9.1 rejected, and paid its costs in full.** Reach's differentiator —
zero persisted state, nothing to provision, works on the first run on a repository it has never seen
— is unchanged and is now contrastable against a named alternative.

Two further points support proceeding:

- **It is not close to usable.** It is in no public SDK, has no documentation, is hidden behind an
  environment variable, and its entire engine is an **unpublished private package** whose absence
  currently blocks Microsoft's own rollout. Nobody outside Microsoft can run this today, and nothing
  in the public record commits to when they can.
- **M1 is two weeks.** Its purpose is to prove the walking skeleton and produce the measurements PRD
  §11 says are the real viability question. Nothing in this finding changes the build-to-test ratio
  or the over-selection risk, which remain the things that could actually kill the project.

### 9.2 The honest counterweight

Three things genuinely got harder, and the PRD should say so rather than absorb them quietly.

**Coverage is more precise, and Reach's biggest technical risk is precisely where coverage wins.**
PRD §11 names over-selection through interface dispatch in layered DI codebases as the main risk —
potentially 80–90% of the suite, making the analysis pure overhead. A coverage map has no such
problem: it records the implementation that actually ran. If Reach's over-selection number comes back
bad on real client solutions, "but we need no infrastructure" is a weaker answer than it looks,
because the infrastructure ask here is a NuGet package, a `global.json` block and a pipeline cache
task — not obviously a week of work.

**"An afternoon to install, no infrastructure" is a narrower moat than the PRD assumes.** Once the
extension is public with its filesystem provider, adoption on Azure DevOps is a `Cache@2` task and a
config block. The moat holds best for: the *secondary* audience (a consultant on a client repo they
cannot change), first-run and no-history situations, non-Azure CI, and non-MTP repositories — which,
per research 01 §7, is still most of the .NET world on the .NET 10 SDK.

**The blind-spot argument cuts both ways and should be stated honestly, not as a selling point.**
Reach's blind spots are reflection, plugin loading and convention-based DI, mitigated by hand-written
framework models (PRD §6) — a permanent maintenance cost. Coverage has none of those blind spots. Its
blind spot is a test that has not run under instrumentation, and stale maps. "Different blind spots"
is true; "our blind spots are better" is not established and should not be claimed.

### 9.3 What should actually change

Nothing in M1's scope. Specifically:

- **Do not** change the technique, the stateless invariant, or the walking-skeleton plan. ADR-0001
  stands and is strengthened.
- **Do** amend PRD §9.1 to name this feature as the concrete instance of the rejected approach, with
  the pipeline shape as evidence. That converts an argument into a citation.
- **Do** add it to PRD §11 as a strategic risk with a trigger: *if the extension is published publicly
  with a filesystem storage provider, re-open the positioning question.* Watch `dotnet/sdk#55405`
  (still open), and NuGet for a `Microsoft.Testing.Extensions.*` affected-test package.
- **Do not** design for map interop. Research 01 §11 asked whether the map is a format Reach could
  emit or consume. The answer is: **there is no published format, and Microsoft is explicitly refusing
  to publish one yet.** Building against it would be building against an invention.

### 9.4 One genuinely useful thing to take from this

Independent of the competitive question, RFC 020 and the extension's design confirm that **the right
MTP emission target is a test-node UID list, not a `--filter` string.** The compatibility orchestrator
uses `--filter-uid` with response files; the provider path passes a `TestNodeUidListFilter` in-process
with *"no command-line-length limit"*. That bears directly on research 01's findings about filter
length limits, NUnit's 2000-clause `AssemblySelectLimit`, and the NUnit `~`-clause performance cliff.

Two caveats before this becomes a plan: Reach does not currently run discovery, and UIDs must come
from somewhere; and `ITestExecutionFilterProvider` is `[Experimental("TPEXP")]` and requires a code
change in the consumer's test project, which is an infrastructure ask of exactly the kind Reach
avoids. `--filter-uid` (MTP 1.8.0+) is the plain-CLI half and is the interesting one. Worth its own
issue; not an M1 concern.

### 9.5 The one-paragraph version

`dotnet test --affected-tests` is coverage-based test impact analysis built on runtime instrumentation
and a persisted, pipeline-cached source-to-test map. It is the approach PRD §9.1 rejected, and
Microsoft's own rollout demonstrates the infrastructure cost §9.1 predicted. It is MTP-only,
undocumented, in no public SDK, and its engine is an unpublished private package that currently blocks
Microsoft from enabling it in their own repository. It does not make M1 a poor investment and Reach's
positioning is unchanged — but it does mean the PRD should stop treating "coverage needs
infrastructure nobody will build" as hypothetical, and should be honest that coverage beats Reach
exactly where PRD §11 says Reach is weakest.
