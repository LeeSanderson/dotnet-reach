# xUnit.net v3 4.0.0, Microsoft.Testing.Platform, and what the CI test step must be

Research for [issue 21](../issues/21-ci-for-the-reach-repository.md). Investigated 11 September 2026.
Companion to [CI and publishing](21-ci-and-publishing.md), whose §5 this both confirms and
**partially overturns** for the specific case of `xunit.v3` at its current version.

Machine used: Windows 11, `dotnet --version` → **10.0.303** (SDKs 6.0.428, 8.0.425, 9.0.308,
9.0.318, 10.0.302, 10.0.303 installed). Every experiment ran in a scratch directory outside the
repository against packages restored from nuget.org. **Nothing in the repository working tree was
built, created, or modified**; no branch, no commit. The only repository file touched is this one.

Every claim below is either a quoted primary source with a URL, or a reproduced command with its
real output. Commands shown as `$ …` were run on this machine today.

---

## The short answer

1. **`dotnet test` with no `global.json` is now a hard build error for an `xunit.v3` 4.0.0 project.**
   Not a fallback, not a warning — an error, exit 1, no tests run (§3.1). This is new at 4.0.0 and it
   overturns [21-ci-and-publishing](21-ci-and-publishing.md) §5.3's "doing nothing leaves VSTest".
   Doing nothing now leaves *nothing working*.
2. **So the repo must ship `global.json` with `"test": {"runner": "Microsoft.Testing.Platform"}`**,
   and the CI step is `dotnet test -c Release --no-build`. The two concerns that §2.4 of the earlier
   note wanted kept apart — SDK pinning and test-runner opt-in — are no longer separable: the runner
   line is mandatory, the `sdk` block remains optional.
3. **Zero tests is a failure, exit code 8, and a filter that matches nothing counts as zero tests.**
   Confirmed empirically for both cases (§4). This is the decisive finding for a tool whose entire
   job is to emit a test filter: `reach` returning an empty selection will turn a green build red
   unless the invocation says otherwise. The mitigations are `--ignore-exit-code 8` on the command
   line or `<TestingPlatformCommandLineArguments>` in the project file; both verified (§4.3).
4. **`--filter` still exists and still takes VSTest syntax** — it was *added* at 4.0.0, not removed
   (§2). The xUnit query-filter language lives on `--filter-query` under MTP and on `-filter` under
   the native runner. These are three mutually exclusive filter families.
5. **Running the built executable directly does NOT give you MTP.** By default it gives xUnit's own
   in-process console runner, which has an entirely different option surface and **exits 0 on zero
   tests** (§3.3). The fork is decided at build time by `UseMicrosoftTestingPlatformRunner`, and the
   generated entry point makes it explicit (§3.4). This is the single easiest way to get two
   different exit codes from what looks like the same test run.
6. **`<OutputType>Exe</OutputType>` is mandatory** — omitting it is a hard MSBuild error with a
   bespoke xUnit message (§5.2).
7. **TRX needs no extra package**, but the switch is `--report-xunit-trx`, not `--report-trx` (§5.3).
   Coverage *does* need `Microsoft.Testing.Extensions.CodeCoverage`.

---

## 1. Versions

### 1.1 `xunit.v3` is at 4.0.0 — confirmed

```
$ Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/xunit.v3/index.json"
… 3.2.0, 3.2.1, 3.2.2,
4.0.0-pre.33, 4.0.0-pre.81, 4.0.0-pre.108, 4.0.0-pre.128, 4.0.0-pre.154,
4.0.0
```

| Package | Latest stable | Checked via |
| --- | --- | --- |
| `xunit.v3` | **4.0.0** | `v3-flatcontainer/xunit.v3/index.json` |
| `xunit.v3.core` | 4.0.0 | flat container |
| `xunit.v3.assert` | 4.0.0 | flat container |
| `xunit.v3.templates` | 4.0.0 | flat container |
| `xunit.runner.visualstudio` | **4.0.0** | `v3-flatcontainer/xunit.runner.visualstudio/index.json` (tail: `3.1.5, 4.0.0-pre.3, 4.0.0-pre.4, 4.0.0-pre.5, 4.0.0`) |
| `xunit.analyzers` | 2.0.0 | <https://xunit.net/releases/> |

Release notes: <https://xunit.net/releases/v3/4.0.0>, headed verbatim:

> Core Framework v3 4.0.0 — 2026 August 14
> Today, we're shipping three new releases: xUnit.net Core Framework v3 4.0.0 / xUnit.net
> Analyzers 2.0.0 / xUnit.net Visual Studio adapter 4.0.0
> The last major release (3.0.0) was 13 months ago, and the last minor release (3.2.2) was 7
> months ago.

The GitHub release is `v3-4.0.0`, published 2026-08-15T03:19:27Z
(`gh api repos/xunit/xunit/releases`).

### 1.2 The package is now a thin alias, and the MTP version is baked into the package ID

This is the structural change that explains everything else. `xunit.v3` 4.0.0 contains no code:

```
$ cat ~/.nuget/packages/xunit.v3/4.0.0/xunit.v3.nuspec
  <description>… Installing this package installs xunit.v3.mtp-v2.</description>
  <dependencies>
    <group targetFramework="net8.0">
      <dependency id="xunit.v3.mtp-v2" version="[4.0.0]" />
```

and `xunit.v3.mtp-v2`'s nuspec in turn: *"Installing this package installs xunit.v3.core.mtp-v2,
xunit.v3.assert, and xunit.analyzers."*

There are three flavours, distinguished only by which Microsoft.Testing.Platform they bind:

| Package | Binds | Effect |
| --- | --- | --- |
| `xunit.v3.mtp-v2` (what `xunit.v3` resolves to) | MTP **2.3.3** | MTP-only; VSTest target blocked on .NET 10 SDK |
| `xunit.v3.mtp-v1` | MTP 1.x | legacy; support discontinued at 4.0.0 |
| `xunit.v3.mtp-off` | none | native runner + VSTest bridge, no MTP |

From the 4.0.0 release notes, verbatim:

> With 4.0, we are discontinuing official support for Microsoft Testing Platform v1. The default
> version of Microsoft Testing Platform support now is v2 (currently at version 2.3.3)…
> we will continue to offer packages to turn off Microsoft Testing Platform support as well (for
> those who wish to be able to use VSTest on .NET 10+ SDK).

Confirmed on the wire — the two restores pull different platforms:

```
$ grep '"Microsoft.Testing.Platform/' old322/obj/project.assets.json   # xunit.v3 3.2.2
"Microsoft.Testing.Platform/1.9.1"
$ ls ~/.nuget/packages/microsoft.testing.platform.msbuild/               # xunit.v3 4.0.0
2.3.3
```

So **"upgrading xunit.v3 from 3.2.2 to 4.0.0" is, underneath, "moving from MTP v1 to MTP v2"**, and
MTP v2 is where the VSTest bridge dies on .NET 10.

---

## 2. What 4.0.0 changed about the filter surface

### 2.1 The headline: `--filter` was *added*, not removed

The premise worth correcting up front — nothing was taken away from the filter surface. From
<https://xunit.net/releases/v3/4.0.0>, verbatim:

> We have added `--filter`, which accepts the older VSTest filter syntax. This should assist users
> who are porting from VSTest to Microsoft Testing Platform. xunit/xunit#3466
> We have added `--filter-display-name` and `--filter-not-display-name` as new simple filters. …
> they allow filtering based on display name, which allows the user to specify an individual theory
> data row to run based on its display name.
> We have added `--xunit-list`, which is similar to the `-list` option in the console runner.

### 2.2 Before / after, under MTP (`dotnet test`)

Taken from `dotnet test --help` run against a real project at each version on this machine.

| | 3.2.2 (MTP v1) | 4.0.0 (MTP v2) |
| --- | --- | --- |
| VSTest syntax | — | **`--filter`** (new) |
| xUnit query language | `--filter-query` | `--filter-query` |
| simple: class | `--filter-class` / `--filter-not-class` | same |
| simple: method | `--filter-method` / `--filter-not-method` | same |
| simple: namespace | `--filter-namespace` / `--filter-not-namespace` | same |
| simple: trait | `--filter-trait` / `--filter-not-trait` | same |
| simple: display name | — | **`--filter-display-name` / `--filter-not-display-name`** (new) |
| platform | `--filter-uid` | `--filter-uid` |

Proved by probing the 3.2.2 project with the 4.0.0-only options — MTP rejects an unregistered
option with exit **5**:

```
$ cd old322 && dotnet test --no-build -c Release --filter "FullyQualifiedName~OldTests"
Test run completed with non-success exit code: 5
$ dotnet test --no-build -c Release --filter-display-name "*"
Test run completed with non-success exit code: 5
```

…and the same two options working on the 4.0.0 project:

```
$ cd mtponly && dotnet test --no-build -c Release --filter "FullyQualifiedName~PassingTests"
Test run summary: Passed!   total: 2      (exit 0)
```

The three families are **mutually exclusive**, and 4.0.0 reworded the help to say so. Verbatim from
`dotnet test --help` on 4.0.0 (3.2.2 said only "You cannot use both simple filters and query
filters"):

> Note: … This is categorized as a VSTest filter. You cannot combine query filters, simple filters,
> or VSTest filters.

### 2.3 Before / after, under the native runner (the executable)

| | 3.2.2 | 4.0.0 |
| --- | --- | --- |
| query language | `-filter` | `-filter` |
| VSTest syntax | — | **`-filterVSTest`** (new) |
| display name | — | **`-displayName` / `-displayName-`** (new) |
| parallelism mode | `-parallel` | **`-parallelMode`** (renamed) |
| results | `-ctrf` `-html` `-jUnit` `-nUnit` `-trx` `-xml` `-xmlV1` | **`-result-ctrf` `-result-html` `-result-junit` `-result-nunit` `-result-trx` `-result-xml` `-result-xmlV1`** |

```
$ old322/bin/Release/net10.0/old322.exe -filterVSTest "FullyQualifiedName~OldTests"
error: unknown option: -filterVSTest          (exit 3)
$ old322/bin/Release/net10.0/old322.exe -result-trx x.trx
error: unknown option: -result-trx            (exit 3)
```

The result-writer rename is **soft in the native runner** — the old names still work with a warning,
which is worth knowing because it means a stale CI script degrades loudly rather than silently:

```
$ mtponly/bin/Release/net10.0/mtponly.exe -trx y.trx
The '-trx' switch has been deprecated in favor of '-result-trx' and will be removed in the next major version
…  Total: 3, Errors: 0, Failed: 0    (exit 0, y.trx written)
```

### 2.4 The report switches under MTP renamed, and that rename is *hard*

| 3.2.2 | 4.0.0 |
| --- | --- |
| `--report-ctrf` | `--report-xunit-ctrf` |
| `--report-junit` | `--report-xunit-junit` |
| `--report-nunit` (NUnit v2.5) | `--report-xunit-nunit` (**NUnit v3**) |
| `--report-xunit` | `--report-xunit-xml` |
| `--report-xunit-html` | unchanged |
| `--report-xunit-trx` | unchanged |

Release notes give the reason verbatim: *"Several of the report command line switches have changed,
to help prevent future collisions when reports are added by MTP."* Unlike the native runner, there
is no deprecated alias — all four old names are rejected outright:

```
$ cd mtponly
$ dotnet test --no-build -c Release --report-xunit   ; # → exit 5
$ dotnet test --no-build -c Release --report-junit   ; # → exit 5
$ dotnet test --no-build -c Release --report-ctrf    ; # → exit 5
$ dotnet test --no-build -c Release --report-nunit   ; # → exit 5
```

### 2.5 The query filter language itself is unchanged and predates 4.0

<https://xunit.net/docs/query-filter-language>, verbatim:

> New in v3 is support for an advanced query filter language.
> `/<assemblyFilter>/<namespaceFilter>/<classFilter>/<methodFilter>`
> To filter based on a trait, add an appropriate trait expression to the end of your query, in the
> form of `[name=value]` or `[name!=value]`.
> You can specify a query filter using `--filter-query` expression … You can specify a query filter
> using `-filter` expression

Verified against a real assembly:

```
$ dotnet test --no-build -c Release --filter-query "/*/Alpha/PassingTests/*"
Test run summary: Passed!   total: 2      (exit 0)
$ mtponly.exe -filter "/*/Alpha/PassingTests/*"
   mtponly  Total: 2, Errors: 0, Failed: 0, Skipped: 0    (exit 0)
```

---

## 3. What actually runs under each invocation style

The test project used throughout (`mtponly`) is the minimal modern shape — no
`xunit.runner.visualstudio`, no `Microsoft.NET.Test.Sdk`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="4.0.0" />
  </ItemGroup>
</Project>
```

with three passing tests in namespace `Alpha`, across classes `PassingTests` and `ToggleTests`.

### 3.1 `dotnet test`, no `global.json` — **hard error, nothing runs**

```
$ dotnet test --no-build -c Release
…\microsoft.testing.platform.msbuild\2.3.3\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets(320,5):
error : Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10
SDK and later. If you use dotnet test, you should opt-in to the new dotnet test experience. For more
information, see https://aka.ms/dotnet-test-mtp-error
EXIT=1
```

`dotnet test --help` in this state still prints *".NET Test Command for VSTest"* — so the CLI is
genuinely in VSTest mode, and MTP v2's MSBuild targets refuse to be driven from it.

**Adding the VSTest packages does not help.** A project with `xunit.v3` 4.0.0 **plus**
`xunit.runner.visualstudio` 4.0.0 **plus** `Microsoft.NET.Test.Sdk` 18.10.0 produces the byte-identical
error. The block is imposed by the MTP v2 package, not by a missing adapter:

```
$ cd withvstest && dotnet test --no-build -c Release
…error : Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later…
EXIT=1
```

Microsoft's side of the same statement, <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test>:

> Running MTP projects under VSTest mode is considered legacy in favor of the newer experience in
> .NET 10 SDK. The support of running under this mode will be removed in MTP version 2 if run with
> .NET 10 SDK. The support remains available for .NET 9 SDK and earlier for backward compatibility.

MTP version 2 has arrived, and `xunit.v3` 4.0.0 is what brought it.

**Filter option:** none — the run never starts.
**Exit codes:** 1 in every case (pass, fail, empty). The build fails before discovery.

### 3.2 `dotnet test` with `global.json` MTP opt-in — **MTP host, the useful mode**

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

```
$ dotnet test --help
  .NET Test Command for Microsoft.Testing.Platform (opted-in via 'global.json' file).
  This only supports Microsoft.Testing.Platform and doesn't support VSTest.

$ dotnet test --no-build -c Release
…mtponly.dll (net10.0|x64) passed (1s 052ms)
Test run summary: Passed!   total: 3   failed: 0   succeeded: 3   skipped: 0
EXIT=0
```

**Host:** Microsoft.Testing.Platform 2.3.3, in-process, driven over MSBuild.
**Filter options accepted:** `--filter` (VSTest syntax), `--filter-query` (xUnit query language),
`--filter-class` / `-method` / `-namespace` / `-trait` / `-display-name` and their `-not-` forms,
`--filter-uid`.

| Scenario | Exit | Reproduced |
| --- | --- | --- |
| (a) all pass | **0** | `total: 3, failed: 0` |
| (b) ≥1 failure | **2** | 1-of-2 failing → 2; 3-of-4 failing → still 2 (not a count) |
| (c) filter matched nothing | **8** | `--filter-class "Alpha.NoSuchClass"` and `--filter-query "/*/*/NoSuchClass/*"` both → `Zero tests ran`, 8 |
| (d) project contains no tests | **8** | project with only a non-test class → `Zero tests ran`, 8 |
| all tests skipped | **0** | default `--zero-tests-policy allow-skipped`; summary still reads `Zero tests ran` |
| unregistered option | **5** | `--report-trx` with no TrxReport package |

This is **all-or-nothing per repository.** A project that is not an MTP app fails the whole run:

```
$ cd mtpoff && dotnet test --no-build -c Release       # global.json present
global.json defines test runner to be Microsoft.Testing.Platform. All projects must use that test runner.
The following test projects are using VSTest test runner:
mtpoff.csproj
EXIT=1
```

### 3.3 Running the built executable directly — **xUnit's own runner, NOT MTP**

This is the trap. `dotnet run --project …` and `bin/Release/net10.0/mtponly.exe` are the same thing,
and by default neither is MTP:

```
$ ./bin/Release/net10.0/mtponly.exe --help
xUnit.net v3 In-Process Runner v4.0.0+8bf043c053 (64-bit .NET 10.0.11)
usage: [:seed] [path/to/configFile.json] [options] [filters] [reporter] [resultFormat filename [...]]
```

**Host:** `Xunit.Runner.InProc.SystemConsole.ConsoleRunner` — xUnit's own console runner.
**Filter options accepted:** `-filter` (query language), `-filterVSTest`, `-class` / `-method` /
`-namespace` / `-trait` / `-displayName` and their `-` suffixed negations. The MTP spellings are
rejected: `--filter-query` → `error: unknown option: --filter-query`, exit 3.

| Scenario | Exit | Reproduced |
| --- | --- | --- |
| (a) all pass | **0** | `Total: 3, Errors: 0, Failed: 0` |
| (b) ≥1 failure | **1** | 3 failures out of 4 → still 1 |
| (c) **filter matched nothing** | **0** | `-class "Alpha.NoSuchClass"` → `Total: 0`, exit 0 |
| (d) **project contains no tests** | **0** | `Total: 0`, exit 0 |
| all tests skipped | **0** | `Total: 1, Skipped: 1` |
| unknown option | **3** | `error: unknown option: -bogusOption` |
| `--help` | **2** | prints usage |

`dotnet run --project . -c Release --no-build` reproduces this exactly, including exit 0 on a
filter that matched nothing (arguments go after `--`):

```
$ dotnet run --project . -c Release --no-build -- -class "NoSuch"
   mtponly  Total: 0
EXIT=0
```

**So the same test project, the same empty filter, gives exit 8 through `dotnet test` and exit 0
through `dotnet run`.** Any tooling that shells out to a test run and reads the exit code has to
know which host it is talking to.

### 3.4 Why — the generated entry point, verbatim

xUnit generates `Main` at build time. Default (`UseMicrosoftTestingPlatformRunner` unset), read from
`obj/Release/net10.0/XunitAutoGeneratedEntryPoint.cs`:

```csharp
public static int Main(string[] args)
{
    if (global::System.Linq.Enumerable.Any(args, arg => arg == "--server" || arg == "--internal-msbuild-node"))
        return global::Xunit.MicrosoftTestingPlatform.TestPlatformTestFramework.RunAsync(args, …).GetAwaiter().GetResult();
    else
        return global::Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args).GetAwaiter().GetResult();
}
```

With `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>` the test is
inverted:

```csharp
    if (global::System.Linq.Enumerable.Any(args, arg => arg == "-automated" || arg == "@@"))
        return global::Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args)…;
    else
        return global::Xunit.MicrosoftTestingPlatform.TestPlatformTestFramework.RunAsync(args, …)…;
```

`dotnet test` in MTP mode passes `--internal-msbuild-node`, which is how the default binary ends up
in MTP after all. Flipping the property makes the bare executable an MTP host, with MTP's exit codes:

```
$ cd mtprunner && ./bin/Release/net10.0/mtprunner.exe
Test run summary: Passed!   total: 2                                   EXIT=0
$ ./bin/Release/net10.0/mtprunner.exe --filter-class "NoSuch"
Test run summary: Zero tests ran   total: 0                            EXIT=8
```

The MSBuild property is threaded straight into the code generator —
`~/.nuget/packages/xunit.v3.core.mtp-v2/4.0.0/buildTransitive/xunit.v3.core.mtp-v2.targets`:

```xml
<XunitGenerateEntryPoint … UseMicrosoftTestingPlatformRunner="$(UseMicrosoftTestingPlatformRunner)" />
```

### 3.5 The fourth style, for completeness: a deliberate VSTest project

`xunit.v3.mtp-off` 4.0.0 + `xunit.runner.visualstudio` 4.0.0 + `Microsoft.NET.Test.Sdk` 18.10.0,
**no** `global.json`, works exactly as VSTest always did:

```
$ dotnet test --no-build -c Release
Passed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2 - mtpoff.dll (net10.0)
EXIT=0
```

| Scenario | Exit |
| --- | --- |
| all pass | 0 |
| ≥1 failure | 1 |
| filter matched nothing | **0** (`No test matches the given testcase filter …`) |
| project contains no tests | **0** (`No test is available in …`) |
| `--logger trx` | works, writes `<user>_<machine>_<timestamp>_net10.0.trx` |

---

## 4. Zero tests is a failure — the decisive question

### 4.1 Exit 8 fires on "filter matched nothing", not only "project has no tests"

Both, separately, reproduced on `xunit.v3` 4.0.0 under `dotnet test` MTP mode:

```
$ dotnet test --no-build -c Release --filter-class "Alpha.NoSuchClass"
…mtponly.dll (net10.0|x64) Zero tests ran (526ms)
Test run summary: Zero tests ran
  error: 1
  total: 0   failed: 0   succeeded: 0   skipped: 0
Test run completed with non-success exit code: 8 (see: https://aka.ms/testingplatform/exitcodes)
EXIT=8

$ dotnet test --no-build -c Release --filter-query "/*/*/NoSuchClass/*"
… exit code: 8                                                          EXIT=8

$ cd emptytests && dotnet test --no-build -c Release      # project with no [Fact] at all
… Zero tests ran … exit code: 8                                         EXIT=8
```

xUnit documents the consequence itself, <https://xunit.net/docs/query-filter-language>, verbatim:

> If a query filter ends up filtering out all the tests in a test assembly, then Microsoft Testing
> Platform will fail that test assembly since it fails test assemblies without at least 1 test in
> them. You can tell it not return a failure code in this situation by adding `--ignore-exit-code 8`
> to your command line.

And MS Learn's table, <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-troubleshooting#exit-codes>
(the older `…-exit-codes` URL now redirects here):

> `8` — The exit code `8` indicates that the test session ran zero tests under the strict
> `--zero-tests-policy`.
> `9` — The exit code `9` indicates that the run executed fewer tests than
> `--minimum-expected-tests` requires, including zero tests.

Note the docs' phrasing is misleading: "under the strict `--zero-tests-policy`" reads as though 8
only fires with `--zero-tests-policy strict`. It does not — see §4.2. The default policy rescues
only the *all-skipped* case.

### 4.2 `--zero-tests-policy` is the knob, and its default does not help a bad filter

New in MTP 2.3.0, so new to xUnit users at 4.0.0. Verbatim from `dotnet test --help` on 4.0.0
(absent from 3.2.2's platform options):

> `--zero-tests-policy` — Specifies how a run that executed no tests is treated. Valid values are
> `'allow-skipped'` (the default) which counts skipped tests as run, so only a run where no test was
> found at all fails with exit code 8, and `'strict'` which treats skipped tests as not run, so a run
> where every test was skipped (or no test was found) fails with exit code 8.

So the taxonomy is:

| Situation | discovered | skipped | `allow-skipped` (default) | `strict` |
| --- | --- | --- | --- | --- |
| tests ran | >0 | — | 0 | 0 |
| every test skipped | >0 | all | **0** | **8** |
| filter matched nothing | 0 | 0 | **8** | **8** |
| project has no tests | 0 | 0 | **8** | **8** |

Reproduced, on a project whose only test is `[Fact(Skip = "always skipped")]`:

```
$ dotnet test --no-build -c Release
Test run summary: Zero tests ran    total: 1  skipped: 1              EXIT=0
$ dotnet test --no-build -c Release --zero-tests-policy strict
… exit code: 8                      total: 1  skipped: 1              EXIT=8
```

**The default is no protection for a filter that matched nothing**, because that produces zero
discovered *and* zero skipped.

### 4.3 How to make it not fail — two verified mitigations

```
$ dotnet test --no-build -c Release --filter-class "NoSuch" --ignore-exit-code 8
Test run summary: Zero tests ran   total: 0                            EXIT=0

$ cd emptytests && dotnet test --no-build -c Release --ignore-exit-code 8
Test run summary: Zero tests ran   total: 0                            EXIT=0
```

Or baked into the project file, so every invocation inherits it (verified on a project with no tests
at all):

```xml
<TestingPlatformCommandLineArguments>$(TestingPlatformCommandLineArguments) --ignore-exit-code 8</TestingPlatformCommandLineArguments>
```

```
$ dotnet test --no-build -c Release
Test run summary: Zero tests ran   total: 0                            EXIT=0
```

This is Microsoft's own recommendation,
<https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-run-and-debug>:

> One common use case is for test projects that have all the tests ignored, which will normally exit
> with exit code 8 (the test session ran zero tests). In this scenario, you can add the following
> under a `PropertyGroup` in your project file: `<TestingPlatformCommandLineArguments>$(TestingPlatformCommandLineArguments) --ignore-exit-code 8</TestingPlatformCommandLineArguments>`

There is also an environment variable, `TESTINGPLATFORM_EXITCODE_IGNORE`, taking the same
semicolon-separated list (Learn, exit-codes section). Not tested here.

### 4.4 `--minimum-expected-tests` — separate mechanism, exit 9, and **it does not supersede 8 in 2.3.3**

`--minimum-expected-tests <NUMBER>` is a first-class `dotnet test` option in MTP mode (present in
both 3.2.2 and 4.0.0). Violating it gives **9**, not 8:

```
$ dotnet test --no-build -c Release --minimum-expected-tests 99     # project has 3 tests
Test run summary: Minimum expected tests policy violation, tests ran 3, minimum expected 99
… exit code: 9                                                         EXIT=9
```

**Default: none — effectively 0, and the option is inert at 0.** Passing `0` is accepted silently
and changes nothing:

```
$ dotnet test --no-build -c Release --minimum-expected-tests 0       # 3 tests, all pass
total: 3   succeeded: 3                                                EXIT=0
```

MS Learn claims the two interact:

> An explicit `--minimum-expected-tests` value supersedes `--zero-tests-policy`. Without the minimum
> option, strict zero-test handling continues to use exit code `8`.

**This is not what MTP 2.3.3 actually does.** Tested both ways — zero tests still yields 8, never 9,
regardless of the minimum:

```
$ dotnet test --no-build -c Release --filter-class "NoSuch" --minimum-expected-tests 1
… exit code: 8                                                         EXIT=8
$ dotnet test --no-build -c Release --filter-class "NoSuch" --minimum-expected-tests 0
… exit code: 8                                                         EXIT=8
$ cd emptytests && dotnet test --no-build -c Release --minimum-expected-tests 1
… exit code: 8                                                         EXIT=8
```

So on the MTP version that `xunit.v3` 4.0.0 pins, **the zero-tests check runs first and wins**;
`--minimum-expected-tests` only produces 9 when at least one test ran but too few. Treat the Learn
sentence as describing a later MTP (2.4.x) — see "What I could not establish".

The 8/9 split is not new: `xunit.v3` 3.2.2 on MTP 1.9.1 behaves identically (empty filter → 8,
`--minimum-expected-tests 99` → 9).

---

## 5. Recommended CI invocation, and the project properties

### 5.1 What the docs say

xUnit publishes no dedicated CI page. Its getting-started page,
<https://xunit.net/docs/getting-started/v3/getting-started>, verbatim:

> `OutputType` is set to `Exe`, because unit test projects in xUnit.net v3 are stand-alone
> executables that can be directly run.
> `TestingPlatformDotnetTestSupport` is set to `true` to align with the default `dotnet test` runner
> being Microsoft Testing Platform. This value is used for .NET 8/9 SDK. The template also
> generates/updates `global.json`, used by .NET 10+ SDK to instruct it to use Microsoft Testing
> Platform.
> The default test project template allows using `dotnet test` to run your test projects, via
> Microsoft Testing Platform.

Microsoft's CI guidance,
<https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-run-and-debug>,
offers three shapes, verbatim:

> To run a single test project in CI, add one step for each test executable that you wish to run,
> such as the following on Azure DevOps: `script: '.\Contoso.MyTests\bin\Debug\net10.0\Contoso.MyTests.exe'`
> Run the `dotnet test` command manually, similar to the typical local workflow: `script: 'dotnet test'`
> Run using the `DotNetCoreCLI` Azure task with test command. This requires a `global.json` file in
> your repository root that specifies MTP as the test runner

and frames `dotnet run` as local-only:

> The `dotnet run` command can be used to build and run your test project. This is the easiest,
> although sometimes slowest, way to run your tests. Using `dotnet run` is practical when you're
> editing and running tests locally, because it ensures that the test project is rebuilt when needed.

**For this repo the choice is effectively made**: `dotnet test` with `global.json`. Invoking the
executable directly would silently opt into exit-0-on-zero-tests (§3.3) unless
`UseMicrosoftTestingPlatformRunner` is also set.

Concretely:

```yaml
- run: dotnet build -c Release
- run: dotnet test -c Release --no-build --report-xunit-trx --results-directory ./TestResults
```

with `global.json` at the repo root containing at minimum:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

### 5.2 `<OutputType>Exe</OutputType>` is required — hard error, not a warning

```
$ dotnet build -c Release        # csproj with no OutputType
…\xunit.v3.core.mtp-v2\4.0.0\buildTransitive\xunit.v3.core.mtp-v2.targets(15,5): error :
xUnit.net v3 test projects must be executable (set project property '<OutputType>Exe</OutputType>').
If this is not a test project, reference xunit.v3.extensibility.core instead.
1 Error(s)
```

The same target also enforces `UseAppHost`, though the package's own props set it for you:

```xml
<Error Text="xUnit.net v3 test projects must build an app host (set project property
  '<UseAppHost>true</UseAppHost>')…" Condition=" '$(UseAppHost)' != 'true' AND … " />
```

Properties the 4.0.0 template sets (from `src/xunit.v3.templates/…/TestProject.csproj` at tag
`v3-4.0.0`), and what they mean here:

| Property | Template default | Relevance to a net10.0 repo |
| --- | --- | --- |
| `OutputType` | `Exe` — always | **required** |
| `TestingPlatformDotnetTestSupport` | `true`, but **skipped when `TargetFramework` is net10.0** | not needed; `global.json` supersedes it on SDK 10 |
| `UseMicrosoftTestingPlatformRunner` | **not set** (only with `--command-line mtp`) | set it only if you want the bare exe to be an MTP host |
| `UseAppHost` | forced `true` by the package props | nothing to do |
| package | `xunit.v3.mtp-v2` (`--test-runner` default `mtp-v2`) | what plain `xunit.v3` resolves to |

There is **no `EnableMSTestRunner` analogue** in xUnit — MTP is selected by package flavour, not by a
property. The template's post-action, condition `TestRunner != "vstest"`, is *"Create or update
'global.json' file required by Microsoft.Testing.Platform."*

### 5.3 TRX and coverage under each host

| Want | MTP (`dotnet test`) | Native exe / `dotnet run` | VSTest (`mtp-off`) |
| --- | --- | --- | --- |
| TRX | `--report-xunit-trx` (+ `--report-xunit-trx-filename`) — **no extra package** | `-result-trx <file>` — no extra package | `--logger trx` |
| TRX, MS flavour | `--report-trx` — needs `Microsoft.Testing.Extensions.TrxReport` | n/a | n/a |
| Coverage | `--coverage --coverage-output-format cobertura` — needs `Microsoft.Testing.Extensions.CodeCoverage` | n/a | `--collect "XPlat Code Coverage"` |
| JUnit / NUnit / HTML / CTRF / xUnit XML | `--report-xunit-junit` / `-nunit` / `-html` / `-ctrf` / `-xml` — built in | `-result-junit` / `-result-nunit` / `-result-html` / `-result-ctrf` / `-result-xml` | n/a |

All verified:

```
$ dotnet test --no-build -c Release --report-xunit-trx --results-directory ../trx-mtp
total: 3  succeeded: 3                                                 EXIT=0
$ ls ../trx-mtp
leeco_CODULEESAN1_2026-09-11_15_07_32.641258.trx

$ dotnet test --no-build -c Release --report-trx       # TrxReport not referenced
… exit code: 5                                                         EXIT=5
$ dotnet test --no-build -c Release --coverage         # CodeCoverage not referenced
… exit code: 5                                                         EXIT=5
```

…and both working once the packages are added (`Microsoft.Testing.Extensions.TrxReport` 2.4.0,
`Microsoft.Testing.Extensions.CodeCoverage` 18.11.2):

```
$ cd withext && dotnet test --no-build -c Release --report-trx     ; # EXIT=0
$ cd withext && dotnet test --no-build -c Release --coverage       ; # EXIT=0
```

MS Learn explains the exit-5 pattern,
<https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-troubleshooting>:

> An extension-specific command-line option can fail with exit code 5 when a test application
> doesn't register the package that provides the option. For example, `--report-trx` requires
> `Microsoft.Testing.Extensions.TrxReport`… MTP core doesn't include report, code coverage, dump,
> retry, or other extension options.

**Note the name collision.** `--report-xunit-trx` (xUnit's own writer, built in) and `--report-trx`
(Microsoft's extension) both produce TRX and are not interchangeable. For this repo the built-in one
is the obvious choice: one fewer package, and the release notes say the 4.0 renames exist precisely
*"to help prevent future collisions when reports are added by MTP."*

---

## 6. Is the VSTest bridge dead?

Not removed — but on .NET 10 SDK with the default package it is unreachable.

- **`xunit.runner.visualstudio` 4.0.0 shipped the same day and does support v3 4.0.** Its nuspec
  description, read from the restored package: *"Visual Studio 2022+ Test Explorer runner for the
  xUnit.net framework. Capable of running xUnit.net v1, v2, and v3 tests."* And
  <https://xunit.net/releases/visualstudio/4.0.0>, verbatim: *"This package supports xUnit.net v3 4.0
  test projects, including the updated configuration file schema and Native AOT test projects."*
- **It is optional.** <https://xunit.net/docs/getting-started/v3/microsoft-testing-platform>, verbatim:
  > Unlike our support for VSTest, our support for Microsoft Testing Platform is built natively into
  > xUnit.net v3. If you want to rely solely on Microsoft Testing Platform support, you can remove
  > the package references to `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk`. … Once all
  > runners can support Microsoft Testing Platform, then we'll be able to deprecate
  > `xunit.runner.visualstudio`.
- **xUnit says out loud it is on the way out** — <https://xunit.net/releases/visualstudio/4.0.0>:
  > The purpose of this package is to support third party VSTest runners, and Microsoft's TestFx team
  > has been fully focused on Microsoft Testing Framework. At some point in the future, this package
  > will probably be entirely deprecated, since most (if not all) of the major third party runners
  > which support VSTest, also support MTP.
- **What happens if it is absent:** under MTP, nothing — everything in §3.2 ran without it. Under
  plain `dotnet test` with no `global.json`, you get §3.1's error, and **adding it back does not fix
  that** (§3.1). The only way to a working VSTest run on SDK 10 is to swap the package for
  `xunit.v3.mtp-off` (§3.5), which the 4.0 template does under `--test-runner vstest`.

For this repo: **do not reference `xunit.runner.visualstudio` or `Microsoft.NET.Test.Sdk`.** They buy
nothing under MTP, and the mere presence of `Microsoft.NET.Test.Sdk` invites someone to try
`dotnet test` without `global.json` and get a confusing error.

---

## What I could not establish

- **Whether MTP 2.4.x changes the 8-vs-9 precedence.** §4.4 shows that on MTP **2.3.3** (what
  `xunit.v3` 4.0.0 pins), zero tests yields 8 even with `--minimum-expected-tests 1`, contradicting
  Learn's "An explicit `--minimum-expected-tests` value supersedes `--zero-tests-policy`". MTP 2.4.0
  is now the latest stable on nuget.org. I did not build against 2.4.0 to see whether the docs
  describe it. **The empirical result is authoritative for the version this repo would actually
  use**; the discrepancy is a warning that it may change under a future xunit.v3 bump.
- **Exit codes above 13.** Learn's table runs 0–14, with 14 (coverage threshold) noted as tied to
  "MTP 2.4 preview". I observed only 0, 1, 2, 3, 5, 8, 9 on 2.3.3. Codes 4, 6, 7, 10–14 were not
  exercised.
- **Behaviour on `ubuntu-latest`/`windows-latest` runner SDKs.** Everything here is SDK **10.0.303**
  on Windows. The runner images also carry 10.0.111, 10.0.204 and 10.0.400
  ([21-ci-and-publishing](21-ci-and-publishing.md) §2.2). The MTP-vs-VSTest block in §3.1 comes from
  the *package's* MSBuild targets rather than the SDK, so it should be SDK-band-independent, but I
  could not test 10.0.400.
- **Whether the exit-8 behaviour differs for a multi-project solution** where one project has zero
  tests and others pass. All runs here were single-project. Worth checking before Reach's own suite
  grows past one test project, since `dotnet test --solution` aggregates.
- **`TESTINGPLATFORM_EXITCODE_IGNORE`.** Documented on Learn as an environment-variable equivalent of
  `--ignore-exit-code`; not exercised.
- **The xUnit MTP documentation page is stale.** <https://xunit.net/docs/getting-started/v3/microsoft-testing-platform>
  still shows "xunit.v3 (version 3.1.0)" samples and the pre-4.0 report switches
  (`--report-ctrf`, `--report-junit`, `--report-nunit`, `--report-xunit`); its last commit is
  2026-05-27, before the 2026-08-14 release. Where that page and the 4.0.0 release notes disagree,
  I trusted the release notes plus the observed `--help` output, both of which agree with each other.
  There is no `https://xunit.net/docs/getting-started/v3/command-line` page (404).
- **Why `xunit.v3` 3.2.2 + plain `dotnet test` (no `global.json`, no `Microsoft.NET.Test.Sdk`) exits
  0 in silence** — no test output, no error, exit 0. Observed but not diagnosed. It is a 3.x-only
  behaviour and does not affect the 4.0.0 decision, but it is a reminder that a green `dotnet test`
  is not by itself proof that tests ran.

---

## Sources

xUnit.net
- <https://xunit.net/releases/v3/4.0.0> — 4.0.0 release notes (filter additions, report renames, MTP v1 discontinued, MTP v2 2.3.3)
- <https://xunit.net/releases/visualstudio/4.0.0> — adapter 4.0.0, v3 4.0 support, future deprecation
- <https://xunit.net/releases/> — release index
- <https://xunit.net/docs/query-filter-language> — query syntax; the `--ignore-exit-code 8` advice
- <https://xunit.net/docs/getting-started/v3/getting-started> — `OutputType`, `TestingPlatformDotnetTestSupport`
- <https://xunit.net/docs/getting-started/v3/microsoft-testing-platform> — VSTest packages optional (**stale, pre-4.0**)
- <https://xunit.net/docs/getting-started/v3/code-coverage-with-mtp> — coverage with MTP
- <https://xunit.net/docs/nuget-packages-v3>
- <https://github.com/xunit/xunit> — tags `v3-3.2.2`, `v3-4.0.0`; `src/xunit.v3.templates/…/TestProject.csproj`

Microsoft.Testing.Platform
- <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-troubleshooting#exit-codes> — the exit-code table (the `…-exit-codes` URL redirects here)
- <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-cli-options>
- <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-run-and-debug> — CI shapes; `TestingPlatformCommandLineArguments`
- <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-test-reports> — TRX extension
- <https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-code-coverage>
- <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test> — VSTest mode removed in MTP v2 on SDK 10

NuGet
- `https://api.nuget.org/v3-flatcontainer/{xunit.v3,xunit.v3.core,xunit.v3.assert,xunit.v3.templates,xunit.runner.visualstudio,microsoft.net.test.sdk,microsoft.testing.extensions.trxreport,microsoft.testing.extensions.codecoverage}/index.json`

Local package assets (read on this machine)
- `~/.nuget/packages/xunit.v3/4.0.0/xunit.v3.nuspec`
- `~/.nuget/packages/xunit.v3.mtp-v2/4.0.0/xunit.v3.mtp-v2.nuspec`
- `~/.nuget/packages/xunit.v3.core.mtp-v2/4.0.0/buildTransitive/xunit.v3.core.mtp-v2.{props,targets}`
- `~/.nuget/packages/xunit.runner.visualstudio/4.0.0/xunit.runner.visualstudio.nuspec`
- generated `obj/Release/net10.0/XunitAutoGeneratedEntryPoint.cs` from both runner modes
