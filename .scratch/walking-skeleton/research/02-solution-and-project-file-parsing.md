# Solution and project-file parsing without MSBuild

Research for [issue 02](../issues/02-solution-and-project-file-parsing.md). Investigated 2026-09-05
against .NET SDK 10.0.303 (also present: 6.0.428, 8.0.424, 9.0.308, 9.0.317, 10.0.302).

Out of scope by instruction: the PDB source-checksum claim from
[ADR-0003](../../../docs/adr/0003-verify-source-binary-correspondence-via-pdb-checksums.md).
This note covers only *where PDBs land*, not what is inside them.

Evidence is one of three kinds and is labelled as such throughout:

- **Doc** — learn.microsoft.com or a first-party repo README/policy file.
- **Source** — a targets/props/`.cs` file in `dotnet/sdk`, `dotnet/msbuild`,
  `microsoft/vs-solutionpersistence`, or the copy installed at
  `C:\Program Files\dotnet\sdk\10.0.303\`.
- **Verified here** — an experiment run on this machine during this investigation. Nothing in this
  repository was built or modified; the library was driven directly and the CLI used read-only.
  A few facts in §3 and §4 come from a delegated agent's runs on this machine rather than my own —
  those are marked in place and listed in §6.

---

## Answers in one page

| Question | Answer |
|---|---|
| Is `Microsoft.VisualStudio.SolutionPersistence` the right library? | **Yes.** MIT, zero dependencies on `net8.0`, no MSBuild dependency, and it is what `dotnet sln`, MSBuild and NuGet all use. Present in every .NET 9 and .NET 10 SDK checked on this machine; absent from 8.0.424. |
| Supported public package or SDK implementation detail? | Public and listed, but **support is scoped to Visual Studio scenarios**. Practically stable: public API unchanged since 1.0.52 (March 2025). |
| Does it read `.slnf`? | **No.** Solution filters are a separate JSON format the SDK parses itself. Reach must do the same — it is ~10 lines. |
| Can `ProjectReference` be read reliably from raw XML? | **For the common case yes, but it is a heuristic, not a parser.** Property-parameterised paths, conditioned `ItemGroup`s, `Choose/When` and `Import` all defeat it. Treat failure as `Unmappable change` → whole-project selection, never as "no edge". |
| `ReferenceOutputAssembly=false` | Project is **still built**, but there is **no metadata reference *and no copy of the DLL***. ADR-0002's conclusion holds and is strengthened; its parenthetical "the output is copied but no metadata reference exists" is wrong and should be corrected (§2.3). |
| `OutputItemType=Analyzer` | Readable from XML, but it is **not the only** way an analyser/generator arrives — `PackageReference` and bare `<Analyzer>` items also produce them, and `OutputItemType` is generic, not analyser-specific. |
| Where does output land? | Statically predictable **only** for a project with no `OutputPath` override and no `-o`/`--artifacts-path` on the build command. The same `OutputPath` value produces a different layout depending on whether it came from the project file or the command line (§3.3). |
| What must be a command-line input? | Configuration, and whether the build used `-o` or `--artifacts-path` — neither is recoverable from disk. Reach should verify by discovery, never trust its own prediction. |
| Where do PDBs land? | Beside the assembly in `$(OutDir)`, same base name, unless `DebugType` is `embedded` (inside the PE) or `none` (absent). |

---

## 1. Solution files

### 1.1 The package

| Fact | Value | Evidence |
|---|---|---|
| Latest stable | **1.0.52**, published 2025-03-04 | [registration API](https://api.nuget.org/v3/registration5-semver1/microsoft.visualstudio.solutionpersistence/1.0.52.json) |
| All versions | 1.0.9, 1.0.23, 1.0.24, 1.0.26, 1.0.28, 1.0.52 — no prereleases | [flat container index](https://api.nuget.org/v3-flatcontainer/microsoft.visualstudio.solutionpersistence/index.json) |
| Listed / deprecated / vulnerable | listed, **not** deprecated, no advisories | registration index (above) |
| Owners | `Microsoft`, `VisualStudioExtensibility` | [search API](https://azuresearch-usnc.nuget.org/query?q=packageid:Microsoft.VisualStudio.SolutionPersistence) |
| Licence | **MIT** | [nuspec](https://api.nuget.org/v3-flatcontainer/microsoft.visualstudio.solutionpersistence/1.0.52/microsoft.visualstudio.solutionpersistence.nuspec) |
| Repository | <https://github.com/microsoft/vs-solutionpersistence> | nuspec `RepositoryUrl`; also an `AssemblyMetadata` attribute in the shipped DLL (**verified here**) |
| Target frameworks | **`net472` and `net8.0` only — no `netstandard2.0`** | `lib/` layout inside the .nupkg; [csproj](https://github.com/microsoft/vs-solutionpersistence/blob/main/src/Microsoft.VisualStudio.SolutionPersistence/Microsoft.VisualStudio.SolutionPersistence.csproj) sets `<TargetFrameworks>net472;net8.0</TargetFrameworks>` |
| Dependencies | **`net8.0`: none at all.** `net472`: `Microsoft.IO.Redist` 6.0.1, `System.Memory` 4.5.5, `System.Threading.Tasks.Extensions` 4.5.4 | nuspec dependency groups |
| Assembly version | `1.0.0.0` (file version `1.0.52.6595`), PublicKeyToken `b03f5f7f11d50a3a` | **verified here** by reflection over the shipped DLL |

**No MSBuild dependency of any kind.** The shipped assembly's only referenced assemblies are
`System.Runtime`, `System.Runtime.InteropServices`, `System.Collections`, `System.Xml.ReaderWriter`,
`System.Text.Encoding.Extensions`, `System.Memory`, `System.Security.Cryptography`, `System.Linq`
(**verified here**). It is also marked `IsAotCompatible`.

Reach targets `net8.0` per the map, so the `net8.0` asset applies and the dependency count is zero.

### 1.2 Supported public API, or SDK implementation detail?

It is a genuinely public, listed, MIT package, but **the written support policy is narrow**.
From [SUPPORT.md](https://github.com/microsoft/vs-solutionpersistence/blob/main/SUPPORT.md):

> Note that this repo is primarily used for Visual Studio and related products and support will be
> focused on those scenarios.
>
> ## Microsoft Support Policy
> Microsoft support for this software is available only for its use in officially supported products
> such as Visual Studio. Support and servicing is limited to the latest released version.

Caveat: that file still carries its unedited template header (`# TODO: The maintainer of this repo
has not yet edited this file`), so treat it as boilerplate rather than a considered policy.

From [CONTRIBUTING.md](https://github.com/microsoft/vs-solutionpersistence/blob/main/CONTRIBUTING.md):

> The primary goal of this project is to provide shared code that can be utilized across various
> products consuming Microsoft Visual Studio solution files.

> we are currently not accepting external contributions that add new functionality.

The nuspec `<description>` is the literal placeholder `Package Description`, and no README is packed —
so nuget.org shows no usage guidance. That is cosmetic, not a signal about supportability.

**Evidence of practical stability**, which matters more than the policy text:

- The repo uses `Microsoft.CodeAnalysis.PublicApiAnalyzers` with checked-in
  `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` baselines and `EnablePackageValidation`
  ([Directory.Build.props](https://github.com/microsoft/vs-solutionpersistence/blob/main/Directory.Build.props)).
- `PublicAPI.Shipped.txt` on `main` is byte-identical to the same file at tag `v1.0.52` — no public
  API change in ~18 months, despite `main` receiving commits as recently as 2026-09-01.
- Assembly version is pinned at `1.0.0.0` while the file version tracks git height
  ([version.json](https://github.com/microsoft/vs-solutionpersistence/blob/main/version.json)), so
  package updates cause no binding churn.

**Assessment.** The risk is not breakage; it is that Microsoft would not owe Reach a fix.
For a tool whose failure mode is already "fall back to whole-project selection", that is acceptable.

### 1.3 Who else uses it

- **The .NET CLI.** [`SlnFileFactory.cs`](https://github.com/dotnet/sdk/blob/main/src/Cli/dotnet/SlnFileFactory.cs)
  is the shared loader for `dotnet sln`, `dotnet build`, `dotnet test` and `dotnet watch`:
  ```csharp
  ISolutionSerializer serializer = SolutionSerializers.GetSerializerByMoniker(solutionPath)!;
  return serializer.OpenAsync(solutionPath, CancellationToken.None).Result;
  ```
- **Pinned centrally** in [`eng/Versions.props`](https://github.com/dotnet/sdk/blob/main/eng/Versions.props):
  > `<!-- When updating MicrosoftVisualStudioSolutionPersistenceVersion make sure to sync with dotnet/msbuild, dotnet/source-build-externals and NuGet/NuGet.Client -->`
  > `<MicrosoftVisualStudioSolutionPersistenceVersion>1.0.52</MicrosoftVisualStudioSolutionPersistenceVersion>`
- **MSBuild.** [`SolutionFile.cs`](https://github.com/dotnet/msbuild/blob/main/src/Build/Construction/Solution/SolutionFile.cs)
  routes `.slnx` through it always, and `.sln` through it behind the
  `MSBUILD_PARSE_SLN_WITH_SOLUTIONPERSISTENCE` env var
  ([Traits.cs](https://github.com/dotnet/msbuild/blob/main/src/Framework/Traits.cs)).
- **NuGet.Client** also references it.

**Verified here**, on this machine: both `Microsoft.Build.dll` and `dotnet.dll` in
`C:\Program Files\dotnet\sdk\10.0.303\` carry an assembly reference to
`Microsoft.VisualStudio.SolutionPersistence`, and `dotnet.deps.json` lists it as an ordinary
package dependency (`Microsoft.VisualStudio.SolutionPersistence/1.0.52`, asset
`lib/net8.0/...`). The DLL is present at:

```
C:\Program Files\dotnet\sdk\10.0.303\Microsoft.VisualStudio.SolutionPersistence.dll
C:\Program Files\dotnet\sdk\10.0.303\DotnetTools\dotnet-format\...
C:\Program Files\dotnet\sdk\10.0.303\DotnetTools\dotnet-watch\10.0.303\tools\net10.0\any\...
C:\Program Files\dotnet\sdk\9.0.317\Microsoft.VisualStudio.SolutionPersistence.dll
```

Not present in SDK 8.0.424. Reach should take the NuGet package, **not** reach into the SDK
directory — the SDK copy is an implementation detail and absent on .NET 8-only agents.

### 1.4 What it exposes

Public type list, **verified here** by reflection over the shipped 1.0.52 assembly (24 exported types):

```
Microsoft.VisualStudio.SolutionPersistence
  ISolutionSerializer, ISolutionSerializer<T>, ISolutionSingleFileSerializer<T>
Microsoft.VisualStudio.SolutionPersistence.Model
  SolutionModel, SolutionItemModel, SolutionProjectModel, SolutionFolderModel,
  ProjectType, ConfigurationRule (struct), BuildDimension (enum), PropertiesScope (enum),
  PropertyContainerModel, SolutionPropertyBag, StringTable, VisualStudioProperties (struct),
  ISerializerModelExtension, ISerializerModelExtension<T>,
  SolutionException, SolutionArgumentException, SolutionErrorType (enum)
Microsoft.VisualStudio.SolutionPersistence.Serializer
  SolutionSerializers (static)
  SlnV12.SlnV12Extensions, SlnV12.SlnV12SerializerSettings, Xml.SlnxSerializerSettings
```

Entry point ([SolutionSerializers.cs](https://github.com/microsoft/vs-solutionpersistence/blob/main/src/Microsoft.VisualStudio.SolutionPersistence/Serializer/SolutionSerializers.cs)):

```csharp
SolutionSerializers.SlnFileV12   // ISolutionSingleFileSerializer<SlnV12SerializerSettings>
SolutionSerializers.SlnXml       // note: SlnXml, NOT "Slnx"
SolutionSerializers.Serializers  // IReadOnlyCollection<ISolutionSerializer>, exactly 2
SolutionSerializers.GetSerializerByMoniker(string) // returns null for unknown extension
```

The members Reach needs, off `SolutionProjectModel`:

| Member | Type | Note |
|---|---|---|
| `FilePath` | `string` | Relative to the solution file. See separator caveat below. |
| `Extension` | `string` | e.g. `.csproj`. **The reliable discriminator.** |
| `TypeId` | `Guid` | Project type GUID. Reliable. |
| `Type` | `string` | Friendly name, **often empty** — see below. |
| `DisplayName` / `ActualDisplayName` | `string` | The solution's name for the project. |
| `Parent` | `SolutionFolderModel?` | Solution folder, `Path` like `/tests/`. |
| `Dependencies` | `IReadOnlyList<SolutionProjectModel>` | Solution-level `ProjectDependencies` — **build order, not `ProjectReference`**. |
| `GetProjectConfiguration(buildType, platform)` | `(string? BuildType, string? Platform, bool Build, bool Deploy)` | Resolved per-project mapping, including the `Build.0` flag. |

And on `SolutionModel`: `SolutionProjects`, `SolutionFolders`, `SolutionItems`, `BuildTypes`
(e.g. `Debug`, `Release`), `Platforms` (e.g. `Any CPU`, `x64`), `FindProject(string)`,
`VisualStudioProperties`.

**Reading is async-only.** `ISolutionSerializer.OpenAsync(string moniker, CancellationToken)` and
`ISolutionSingleFileSerializer<T>.OpenAsync(Stream, CancellationToken)`. There is no sync overload;
both the SDK and MSBuild block on it. Internally the `.slnx` path is synchronous and returns
`Task.FromResult(...)`.

### 1.5 Verified behaviour

Run against the shipped 1.0.52 assembly with hand-written solution files whose projects **do not
exist on disk**.

**It never touches the referenced project files.** Both a `.sln` and a `.slnx` naming four
non-existent projects parsed cleanly and returned every project. This matters for Reach: the
solution can be read before, or instead of, any build. (Corroborated at source: the only filesystem
calls in `src/` are `File.OpenRead` on the solution itself and the write path; there is no
`File.Exists` anywhere.)

**Path separators are host-platform, and paths are not canonicalised.**

| In the file | `FilePath` on Windows |
|---|---|
| `.sln`, `"src\B\B.csproj"` | `src\B\B.csproj` |
| `.sln`, `"src/A/A.csproj"` | `src/A/A.csproj` — **forward slashes preserved** |
| `.slnx`, `Path="src/Lib/Lib.csproj"` | `src\Lib\Lib.csproj` |
| `.slnx`, `Path="./src/P3/P3.csproj"` | `.\src\P3\P3.csproj` — **`./` prefix preserved** |

The serializer converts backslash → host separator
([PathExtensions.cs](https://github.com/microsoft/vs-solutionpersistence/blob/main/src/Microsoft.VisualStudio.SolutionPersistence/Utilities/PathExtensions.cs):
*"Converts a serialized path that uses backslashes to a model path that uses the platform's
directory separator"*). On Windows that leaves forward slashes alone. **Reach must do its own
`Path.GetFullPath(Path.Combine(solutionDir, filePath))` and must not string-compare raw values.**

**`Type` is empty whenever the type is implied by the file extension.** Observed:

| File | `Type` | `TypeId` |
|---|---|---|
| `.sln` C# project (`9A19103F-…`) | `Common C#` | `9a19103f-16f7-4668-be54-9a1e7a4f7556` |
| `.slnx` `.csproj`, no `Type` attribute | `""` | `fae04ec0-301f-11d3-bf4b-00c04f79efbc` |
| `.slnx` `Type="C#"` | `C#` | `fae04ec0-…` |
| `.sln` unknown GUID | the GUID as a string | the GUID |

Note the **type GUID differs between formats for the same project**: `.sln` files written by modern
VS use the SDK-style C# GUID `9A19103F-…`, while `.slnx` infers the legacy `FAE04EC0-…` from the
extension. `TypeId` is therefore **not** an "is this SDK-style?" test. Use `Extension`.
The GUID→name table is `internal`
([ProjectTypeTable.BuiltInTypes.cs](https://github.com/microsoft/vs-solutionpersistence/blob/main/src/Microsoft.VisualStudio.SolutionPersistence/Model/ProjectTypeTable.BuiltInTypes.cs));
the public `SolutionModel.ProjectTypes` contains only types *declared in the file* and was empty for
every solution tested.

**Per-project configuration mapping works, and includes built-in defaults.** For a `.sln` declaring
`Debug|Any CPU` and `Release|Any CPU` where one project deliberately had no `Release ... Build.0` line:

```
Lib.Tests  Debug|Any CPU  -> 'Debug'|'Any CPU'  build=True
Lib.Tests  Release|Any CPU-> 'Release'|'Any CPU' build=False   <-- missing Build.0 detected
```

For a solution with `Debug|Any CPU` and `Release|x64`, a project mapped only to `Debug|Any CPU`
returned `('', '', build=False)` for the unmapped combinations. A `.shproj` and a `.sqlproj`
returned `build=False` for every combination (built-in project-type rules), and a `.vcxproj` in a
`.slnx` mapped solution-`Any CPU` to project-`x64`. So `GetProjectConfiguration` answers two
questions Reach needs at once: *is this project built in this solution configuration*, and
*under which project configuration/platform* — which is what determines the output folder (§3.1).

Platform strings are normalised differently per format: `.sln` yields `Any CPU`, `.slnx` yields
`AnyCPU`. Do not compare across formats.

**Solution-level `ProjectDependencies` are exposed** via `SolutionProjectModel.Dependencies`.
These are explicit build-order edges, **not** `ProjectReference`s, and are a distinct (and rare)
source of project-graph edges. Reach should read them — they can only widen.

**Error behaviour is clean and distinguishable:**

| Input | Result |
|---|---|
| garbage `.sln` | `SolutionException: Not a solution file.` |
| malformed `.slnx` | `System.Xml.XmlException` |
| missing file | `System.IO.FileNotFoundException` |
| `x.csproj` / `x.slnf` moniker | `GetSerializerByMoniker` returns **`null`** (does not throw) |
| `x.SLN` | matched — extension test is case-insensitive |

### 1.6 `.slnx` status — Reach must support it from day one

`.slnx` is not a preview format, and on .NET 10 it is what new solutions get by default.

> The .NET SDK added support for SLNX files in version 9.0.200, and it's proven to be a stable,
> understandable format for developers. It's well-supported by all major .NET tooling and is much
> easier for developers to maintain.

> In .NET 10, `dotnet new sln` generates an SLNX-format solution file instead of an SLN-formatted
> solution file.

— <https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default>
(the escape hatch is `--format sln`).

MSBuild support is 17.12 and later
(<https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference>). Visual
Studio exposes a "Default Solution File Format" setting in VS 2026
(<https://learn.microsoft.com/en-us/visualstudio/ide/projects-and-solutions-options-dialog-box>).

`dotnet sln migrate` writes a sibling `.slnx` and leaves the `.sln` in place
(<https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln>), so a repository mid-migration
has both files — see §1.8 point 4.

**Consequence.** Any `.sln`-only design would be obsolete on arrival. This is the single strongest
argument for the library over hand-rolling: one API reads both, and the SDK, MSBuild and NuGet all
route through it, so Reach's reading of a solution agrees with the build's by construction.

### 1.7 The gap: `.slnf` solution filters

`SolutionPersistence` 1.0.52 has exactly two serializers and **`.slnf` is not one of them**
(**verified here**). The SDK parses solution filters itself with `System.Text.Json`
(`CreateFromFilteredSolutionFile` in `SlnFileFactory.cs`).

The format is documented at
<https://learn.microsoft.com/en-us/visualstudio/msbuild/solution-filters>:

> Solution filter files are JSON files with the extension `.slnf` that indicate which projects to
> build or load from all the projects in a solution.

```json
{
  "solution": {
    "path": "..\\..\\Documents\\GitHub\\msbuild\\MSBuild.sln",
    "projects": [ "src\\Build.OM.UnitTests\\Microsoft.Build.Engine.OM.UnitTests.csproj" ]
  }
}
```

Two rules from that page that Reach must implement correctly:

> The path to the solution file is relative to the location of solution filter file, but the paths
> to each project are relative to the solution file itself and should match the project paths in the
> solution file.

> When building a solution filter from the command line, MSBuild automatically follows dependencies.
> It builds a project if it's specified in the filter or referenced by a project that is built.

And a trap:

> In the case where you're using the `.slnx` solution file format, supported in MSBuild 17.12 and
> later, the `.slnx` file takes priority over the `.slnf` file.

**Implication for Reach.** A `.slnf` narrows the *build scope*, not the analysis scope — the
transitive closure is still built. Per ADR-0002, Reach's analysis scope is the union of test-project
closures, so a filter that omits a test project legitimately shrinks the scope, but a filter that
omits a *dependency* does not, because MSBuild builds it anyway. Reading a `.slnf` is ~10 lines of
`System.Text.Json` and is worth doing rather than erroring on the extension.

### 1.8 The alternative: `dotnet sln list`

Documented at <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln>. It accepts
`.sln`, `.slnx` **and `.slnf`** ("Support for *.slnf* files was added in .NET SDK 9.0.3xx").
**Verified here** on SDK 10.0.303 — identical output for `.sln` and `.slnx`, correctly filtered for
`.slnf`, and it does not require the projects to exist:

```
Project(s)
----------
src\P1\P1.csproj
tests\T1\T1.csproj
```

Reject it, for five reasons:

1. **No machine-readable output.** The full option set is a single `--solution-folders` flag
   ([SolutionListCommandDefinition.cs](https://github.com/dotnet/sdk/blob/main/src/Cli/Microsoft.DotNet.Cli.Definitions/Commands/Solution/SolutionListCommandDefinition.cs)).
   No `--format`, no `--json`.
2. **The header is localised.** `ProjectsHeader` = `Project(s)` lives in `CliCommandStrings.resx`
   with `.xlf` translations. A parser would have to skip two lines by position and hope.
3. **It gives paths only.** No project type, no per-configuration build flag, no solution folders
   alongside the projects (`--solution-folders` replaces the project list rather than adding to it —
   **verified here**).
4. **It fails when both `Foo.sln` and `Foo.slnx` exist** in a directory with no argument — exactly
   the state `dotnet sln migrate` leaves behind, since migrate writes a sibling and leaves the
   original in place. MSBuild has the same rule
   (<https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-command-line-reference>):
   > Both `.sln` and `.slnx` files for the same solution can be present in the same directory; if
   > both are present, you must explicitly specify one of them to build the solution.

   Reach must therefore require an explicit solution path, or apply its own documented precedence,
   rather than "find the solution in this directory".
5. **It is a process launch per solution**, against a library call that costs nothing.

Hand-rolling both parsers is also rejected: `.sln` is a quirky legacy text format whose
configuration-mapping semantics (`ActiveCfg` vs `Build.0` vs `Deploy.0`, project-type default rules)
are exactly the part Reach would get subtly wrong, and Microsoft has already written and shipped
that code under MIT with no dependencies.

### 1.9 Recommendation

**Take `Microsoft.VisualStudio.SolutionPersistence`**, pinned to 1.0.52, plus **a ~10-line
`System.Text.Json` reader for `.slnf`**. Wrap both behind a port (`ISolutionReader` →
`IReadOnlyList<ProjectInSolution>`) so the dependency is replaceable and testable, consistent with
the map's "every environment boundary behind a port".

Encode these five behaviours in the adapter, each covered by a test:

1. Resolve `FilePath` to an absolute canonical path against the solution directory.
2. Discriminate project kind by `Extension`, never by `TypeId` or `Type`.
3. Use `GetProjectConfiguration` to skip projects with `Build=false` for the requested
   configuration, and to learn the project's own configuration/platform.
4. Read `Dependencies` as extra project-graph edges.
5. Map `SolutionException` / `XmlException` to a loud Reach error — an unparseable solution means
   analysis scope cannot be established, and ADR-0002 says Reach must error rather than answer
   narrowly.

---

## 2. Project files: reading `ProjectReference` from raw XML

### 2.1 What the XML can legally contain

A static reader sees text; MSBuild sees an evaluated project. The gap is real and documented.
From <https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-items>:

- `Include` is a **semicolon-delimited list**, not a single path.
- `Include` supports `*`, `**` and `?` wildcards.
- Relative paths resolve against **the project directory**, even for items declared in an imported
  `.targets` file:
  > The `Include` attribute is a path that is interpreted relative to the project file's folder …
  > even if the item is in an imported file such as a `.targets` file.

  This is the opposite of `<Import>`, whose paths resolve against the importing file
  (<https://learn.microsoft.com/en-us/visualstudio/msbuild/import-element-msbuild>).
- Items can be created or modified **during execution**, inside targets, not only at evaluation.
- `Condition` may appear on the item, on the enclosing `ItemGroup`, or the whole group may sit
  inside `Choose`/`When`/`Otherwise`
  (<https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-conditional-constructs> — its
  own example puts `<Reference>` and `<Compile>` items inside a `<When>`).
- `<Import Project="...">` itself **accepts wildcards**, and imports are ordered.
- `$(Prop)`, `@(Item)`, `%(Meta)` and property functions (`$([System.DateTime]::Now…)`) can all
  appear inside an attribute value.
- Environment variables are properties, so `$(PATH)` and friends resolve from the environment.

Statically resolvable without any evaluation
(<https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties>,
all **Reserved**, i.e. not overridable):

`MSBuildProjectName` (file name without extension), `MSBuildProjectDirectory` (**no** trailing
backslash), `MSBuildProjectFile`, `MSBuildProjectFullPath`, `MSBuildProjectExtension`,
`MSBuildThisFileDirectory` (**with** trailing backslash), `MSBuildThisFileName`, `MSBuildThisFile`.

Not statically resolvable: `MSBuildExtensionsPath`, `MSBuildToolsPath`, `MSBuildSDKsPath`,
`MSBuildRuntimeType`, `MSBuildStartupDirectory` (depends on invocation cwd), `OS`, `VsInstallRoot`.

**Practical consequence.** A raw-XML reader is a *heuristic with a known failure set*. Reach should
detect the failure conditions explicitly — any `$(`, `@(`, `%(` in an `Include`; any `Condition` on
the item, the `ItemGroup`, or an ancestor; any `Choose`; any `<Import>` other than the ones Reach
itself follows — and treat a project whose references it cannot fully resolve as producing an
**Unmappable change** → whole-project selection for everything downstream. Silently returning a
short edge list is the under-selection bug.

### 2.2 How common are those hard cases?

**No first-party source establishes this.** No Microsoft telemetry publication, survey or guidance
document makes any claim about the prevalence of property-parameterised `ProjectReference` paths.
Do not put a frequency figure in the spec on the strength of documentation.

A **local, small, non-representative** sample was taken on this machine for orientation only:
470 `.csproj` files (excluding `bin`/`obj`/`node_modules`/`packages`/`artifacts`) across 40
repository roots under `C:\Dev` — 420 SDK-style, 50 legacy.

| Pattern | Count |
|---|---|
| `ProjectReference` elements total | 798 (in 335 files) |
| …containing `$(` anywhere in the element | **0** |
| …with a `Condition` attribute on the element | **0** |
| …inside an `<ItemGroup Condition=…>` | **0** |
| …with a wildcard in `Include` | **0** |
| …with `ReferenceOutputAssembly="false"` | 4 |
| …with `OutputItemType="Analyzer"` | 4 |
| SDK-style projects setting `<AssemblyName>` | 77 / 420 (18%) — but 42 of those come from just two repositories |
| SDK-style projects setting `<OutputPath>`/`<BaseOutputPath>` | 11 / 420 (2.6%) |
| Legacy projects setting `<AssemblyName>` and `<OutputPath>` | 50 / 50 (as expected — the old templates always did) |

Read this as "the simple case dominates in one engineer's checkout", nothing stronger. It does not
license Reach to assume the hard cases away; it does suggest the raw-XML approach will succeed on
the overwhelming majority of projects, with a well-defined fallback for the rest.

### 2.3 `ReferenceOutputAssembly=false`

Documented at
<https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items>:

> **`ReferenceOutputAssembly`** Optional boolean. If set to `false`, doesn't include the output of
> the referenced project as a Reference of this project, but still ensures that the other project
> builds before this one. Defaults to `true`.

Confirmed against the targets. In `Microsoft.Common.CurrentVersion.targets` the default is
materialised in `AssignProjectConfiguration`, not an `ItemDefinitionGroup` (local SDK copy,
lines 1622 and 1626):

```xml
<ReferenceOutputAssembly Condition="'%(ProjectReferenceWithConfiguration.ReferenceOutputAssembly)' == ''">true</ReferenceOutputAssembly>
```

In `ResolveProjectReferences`, the `MSBuild` task's `Condition` is gated on **`BuildReference`**,
and `ReferenceOutputAssembly` appears only on the `<Output>` capture (local SDK copy, lines
2158-2166; [same on GitHub](https://github.com/dotnet/msbuild/blob/fcb368d8894f6448382f20304c659f383d210f61/src/Tasks/Microsoft.Common.CurrentVersion.targets#L2168-L2183)):

```xml
<Output TaskParameter="TargetOutputs" ItemName="_ResolvedProjectReferencePaths"
  Condition="'%(_MSBuildProjectReferenceExistent.ReferenceOutputAssembly)'=='true' or '$(DesignTimeBuild)' == 'true'" />
```

So the filter is capture-side:

- **The referenced project is still built.** `BuildReference="false"` is the metadata that stops
  that; `ReferenceOutputAssembly="false"` is not.
- **The assembly is not passed to the compiler and is not copied to the output directory.**
  `_ResolvedProjectReferencePaths` is the only route from a project reference into
  `ResolveAssemblyReference`; RAR produces `ReferenceCopyLocalPaths`; `_CopyFilesMarkedCopyLocal`
  copies exactly those to `$(OutDir)`. Skipping step one skips all of them.

**This corrects a premise in ADR-0002.** The ADR says such references are cases "where output is
copied but no metadata reference exists". In fact **neither** happens by default: no metadata
reference *and* no copy. The ADR's *conclusion* is unaffected and arguably strengthened — these
edges are invisible to metadata, so closures must come from the project files — but the parenthetical
should be corrected when the ADR is next touched.

Two separate switches, worth not conflating:

- `ReferenceOutputAssembly="false"` → no assembly reference, no assembly copy.
- `Private="false"` → no transitive `CopyToOutputDirectory` content copy. That path is gated on
  `Private`, not on `ReferenceOutputAssembly` (Microsoft.Common.CurrentVersion.targets,
  `_GetChildProjectCopyToOutputDirectoryItems`), so content **still flows** with
  `ReferenceOutputAssembly="false"`.

Also observed in the local SDK: `.esproj` references get `ReferenceOutputAssembly=false` applied
automatically by the `IgnoreJavaScriptOutputAssembly` target (line 1553), because JS/TS projects
never produce an assembly.

### 2.4 `OutputItemType="Analyzer"`

Documented generically at the same page:

> **`OutputItemType`** Optional string. Item type to emit target outputs into. Default is blank.

Honoured in `ResolveProjectReferences` by using the metadata value as the item name (local SDK copy,
lines 2144-2146 and 2164-2166):

```xml
<Output TaskParameter="TargetOutputs"
  ItemName="%(_MSBuildProjectReferenceExistent.OutputItemType)"
  Condition="'%(_MSBuildProjectReferenceExistent.OutputItemType)' != ''" />
```

and declared blank in the `ItemDefinitionGroup` with a comment stating the intended pairing
(line 2114): *"Extra item type to emit outputs of the destination into. Defaults to blank. To emit
only into this list, set the ReferenceOutputAssembly metadata to false as well."*

**Correction to a common belief: the Roslyn source-generator cookbooks do NOT prescribe this
pattern.** `docs/features/source-generators.cookbook.md`,
`incremental-generators.cookbook.md`, `source-generators.md` and `incremental-generators.md` in
`dotnet/roslyn` contain zero occurrences of `OutputItemType`, `ReferenceOutputAssembly` or
`ProjectReference`. The first-party prescriptions that do exist are:

- [dotnet/runtime project-guidelines.md](https://github.com/dotnet/runtime/blob/eabca98e78db9f203ae1aa9418c0ad0cd4170cab/docs/coding-guidelines/project-guidelines.md):
  > To consume a source generator that isn't provided via a targeting pack, simply add a
  > `<ProjectReference Include="..." ReferenceOutputAssembly="false" OutputItemType="Analyzer" />`
  > item to the project

  and, importantly for a static reader, the multi-targeting caveat:
  > you need to add the `SetTargetFramework="TargetFramework=netstandard2.0"` metadata to the
  > ProjectReference item
- The [Roslyn SDK sample csproj](https://github.com/dotnet/roslyn/blob/0e119d1bc17697c7f8d9a8e142213c8557310c80/src/RoslynSdk/Samples/CSharp/SourceGenerators/GeneratedDemo/CSharpGeneratedDemo.csproj),
  which uses exactly that pair.

**`OutputItemType="Analyzer"` is not the only way an analyser or generator reaches a compilation:**

1. `ProjectReference` + `OutputItemType="Analyzer"` — the local-source variant. Note it is not
   *required* to pair with `ReferenceOutputAssembly="false"`.
2. `PackageReference` to an analyser package. The SDK populates `Analyzer` items from
   `project.assets.json` via `ResolveLockFileAnalyzers`
   ([Microsoft.PackageDependencyResolution.targets](https://github.com/dotnet/sdk/blob/fcd632a06320edcb05ac62f20e150da48b00b6a8/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.PackageDependencyResolution.targets#L497-L501)).
   No `ProjectReference` involved at all.
3. Bare `<Analyzer Include="…dll" />` items. `Microsoft.CSharp.Core.targets` passes `@(Analyzer)`
   to the compiler unconditionally.
4. `OutputItemType` is fully generic — `Analyzer` is not special-cased anywhere. Roslyn's own tests
   use `OutputItemType="ReferencedProjectSourceRoots"`.

**Consequences for Reach.**

- The mandatory rule-table row (source-generator-to-consumers) can be *populated* from XML
  for case 1, which is the case that matters for first-party generators in the working tree.
  Cases 2-4 are either not first-party or not statically visible.
- An `OutputItemType="Analyzer"` edge is a **build-order and compilation-input** edge, not a runtime
  assembly-reference edge. The generator's IL never appears in the consumer's call graph, yet a
  change to the generator changes the consumer's compiled output. It deserves its own
  `Edge provenance` value, or at least its own tier in the unmappable-change rule table.
- A generator project referenced only with `OutputItemType="Analyzer"` is still inside a test
  project's **project closure** per ADR-0002, so it is in analysis scope even though nothing links
  against it.
- Watch for `SetTargetFramework` on such references: the generator is typically built as
  `netstandard2.0` regardless of the consumer's TFM, which affects which output directory its
  assembly lands in.

### 2.5 Implicit and transitive `ProjectReference`s

The SDK **does** add `ProjectReference` items that are not in the XML — not by globbing, but
transitively from the restore assets file, at build time
([Microsoft.PackageDependencyResolution.targets](https://github.com/dotnet/sdk/blob/fcd632a06320edcb05ac62f20e150da48b00b6a8/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.PackageDependencyResolution.targets#L489-L495)):

```xml
<Target Name="IncludeTransitiveProjectReferences"
        DependsOnTargets="ResolvePackageAssets"
        Condition="'$(DisableTransitiveProjectReferences)' != 'true'">
  <ItemGroup>
    <ProjectReference Include="@(_TransitiveProjectReferences)" />
  </ItemGroup>
</Target>
```

Documented at <https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props>:

> The `DisableTransitiveProjectReferences` property controls implicit project references. Set this
> property to `true` to disable implicit `ProjectReference` items.

And on the items page: *"in .NET Core (including .NET 5 and later), project references are
transitive"*, unlike .NET Framework.

**This is good news for Reach.** The implicit items are the transitive closure of the explicit ones,
so a reader that computes reachability over the direct XML edges arrives at the same node set.
Two caveats:

- `PrivateAssets="All"` and `DisableTransitiveProjectReferences` can *prune* the real closure below
  the computed one — Reach over-approximates, which is the safe direction.
- A `PackageReference` to a package that was itself built from a project in the working tree
  produces **no** edge in the XML graph. That is a blind spot, and it is the same blind spot the
  `First-party assembly` definition already handles: such an assembly's debug symbols will not point
  at source inside the working tree, so it is correctly excluded.

The SDK does **not** glob `ProjectReference`. `Microsoft.NET.Sdk.DefaultItems.props` contains zero
occurrences of it, and only three item types are implicit
(<https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview>): `Compile`,
`EmbeddedResource`, `None` — all of which *exclude* `**/*.*proj`.

Whether `ProjectReference Include` may be a **glob** or a **directory** is undocumented either way.
Globbing follows mechanically from item semantics but no first-party sample does it on this item
type; a directory would pass the targets' `Exists('%(Identity)')` filter and then fail inside the
MSBuild task. Reach should resolve `Include` as a semicolon-delimited list, expand wildcards if
present, require the result to be an existing `*proj` file, and warn (and widen) otherwise.

### 2.6 `AssemblyName`

**Default is the project file name without its extension**, and this is stated only in source, never
in the docs.

`Microsoft.NET.Sdk.props` line 41 (local SDK copy;
[GitHub](https://github.com/dotnet/sdk/blob/fcd632a06320edcb05ac62f20e150da48b00b6a8/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.props#L42)):

```xml
<AssemblyName Condition=" '$(AssemblyName)' == '' ">$(MSBuildProjectName)</AssemblyName>
```

`Microsoft.Common.CurrentVersion.targets` repeats it for non-SDK projects and derives the output
file name (local SDK copy, lines 238-247):

```xml
<TargetName Condition="'$(TargetName)' == '' and '$(OutputType)' == 'winmdobj' and '$(RootNamespace)' != ''">$(RootNamespace)</TargetName>
<TargetName Condition=" '$(TargetName)' == '' ">$(AssemblyName)</TargetName>
<TargetFileName Condition=" '$(TargetFileName)' == '' ">$(TargetName)$(TargetExt)</TargetFileName>
```

with `TargetExt` = `.dll` for `library`, `.exe` for `exe`/`winexe`/`appcontainerexe`,
`.netmodule` for `module` (lines 215-220).

`$(MSBuildProjectName)` is **Reserved** and documented as *"The file name of the project file
without the file name extension"* — statically computable, zero evaluation.

**Where it can be overridden**, in increasing order of invisibility to a raw-XML reader:

1. **In the project file.** Directly visible. Possibly inside a `Condition`ed `PropertyGroup`,
   including per-TFM (`Condition="'$(TargetFramework)' == 'net8.0'"`), which only takes effect in
   inner builds.
2. **In `Directory.Build.props`.** Visible only if Reach walks up and reads it. This works because
   `Directory.Build.props` is imported by `Microsoft.Common.props` *before* `Microsoft.NET.Sdk.props`
   sets the conditional default.
3. **Via `-p:AssemblyName=` on the command line.** A global property, which
   <https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties> says *"override
   property values that are set in the project file"* — so it beats even an explicit
   `<AssemblyName>` (unless the project declares `TreatAsLocalProperty`). Completely invisible.
4. **From an arbitrary imported `.props`/`.targets`**, including NuGet-injected ones.

`winmdobj` is a further edge case: `TargetName` comes from `RootNamespace`, not `AssemblyName`.

**Recommendation.** Do not use `AssemblyName` as the join key from project to assembly. Prefer
discovery: enumerate the assemblies actually present in the resolved output directory and identify
first-party ones by their debug symbols (as the map already decided). Use
`AssemblyName ?? MSBuildProjectName` only as a *hint* for locating the directory and for producing
a helpful error when the expected assembly is missing — which, per the map, must be an error and
not a silent narrowing.

### 2.7 `Directory.Build.props`

If Reach reads project properties at all, it must implement the discovery rule.
<https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory>:

> When searching for a *Directory.Build.props* file, MSBuild walks the directory structure upwards
> from your project location `$(MSBuildProjectFullPath)`, stopping after it locates a
> *Directory.Build.props* file.

> The location of the solution file is irrelevant to *Directory.Build.props*.

> For any given project, MSBuild finds the first *Directory.Build.props* upward in the solution
> structure, merges it with defaults, and stops scanning for more.
> - If you want multiple levels to be found and merged, then `<Import…>` the "outer" file from the
>   "inner" file.

> Or more simply: the first *Directory.Build.props* that doesn't import anything is where MSBuild
> stops.

> Linux-based file systems are case-sensitive. Make sure the casing of the *Directory.Build.props*
> filename matches exactly, or it won't be detected during the build process.

Confirmed in the local SDK's `Current\Microsoft.Common.props` lines 19-36:

```xml
<_DirectoryBuildPropsFile Condition="'$(_DirectoryBuildPropsFile)' == ''">Directory.Build.props</_DirectoryBuildPropsFile>
<_DirectoryBuildPropsBasePath Condition="'$(_DirectoryBuildPropsBasePath)' == ''">$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildProjectDirectory), '$(_DirectoryBuildPropsFile)'))</_DirectoryBuildPropsBasePath>
...
<Import Project="$(DirectoryBuildPropsPath)" Condition="'$(ImportDirectoryBuildProps)' == 'true' and exists('$(DirectoryBuildPropsPath)')"/>
```

Two details the doc glosses over: the search seed is `$(MSBuildProjectDirectory)`, not
`$(MSBuildProjectFullPath)`; and both the file name (`$(_DirectoryBuildPropsFile)`) and the whole
search (`$(DirectoryBuildPropsPath)`, `$(ImportDirectoryBuildProps)`) are overridable. Hardcoding
the literal name is right almost always, not universally.

Ordering:

> *Directory.Build.props* is imported early in *Microsoft.Common.props*, and properties defined
> later are unavailable to it.

> *Directory.Build.targets* is imported from *Microsoft.Common.targets* after importing `.targets`
> files from NuGet packages.

Neighbours Reach can ignore:

- **`Directory.Solution.props` / `.targets`** — <https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-solution-build>:
  > When you build a solution file, *Directory.Build.props* and *Directory.Build.targets* aren't
  > imported, so you must use *Directory.Solution.props* and *Directory.Solution.targets* instead.

  These affect only the synthesised solution metaproject, never per-project properties. Irrelevant
  to a project-graph builder.
- **`Directory.Packages.props`** (Central Package Management) — concerns `PackageReference`
  versions only. No bearing on `ProjectReference`.

### 2.8 `ProjectReference` metadata worth recognising

From <https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items>, verbatim
where quoted:

| Metadata | Meaning | Matters to Reach? |
|---|---|---|
| `ReferenceOutputAssembly` | see §2.3 | **Yes** — edge exists, but no metadata reference |
| `OutputItemType` | "Item type to emit target outputs into." | **Yes** — `Analyzer` identifies generators |
| `BuildReference` | "Defaults to `true`. If set to `false`, this ProjectReference will not be built by MSBuild." | **Yes** — the only metadata that removes the build edge |
| `Private` | "Specifies whether the reference should be copied to the output folder." | Yes — governs transitive content copy |
| `SetTargetFramework` | "Sets the global property `TargetFramework` for the referenced project" | **Yes** — changes which output folder holds the assembly |
| `SetConfiguration` / `SetPlatform` | set `Configuration`/`Platform` for the reference | **Yes** — changes the output folder |
| `Targets`, `SkipGetTargetFrameworkProperties`, `GlobalPropertiesToRemove`, `AdditionalProperties`, `UndefineProperties`, `Aliases`, `Name`, `Project` | build mechanics or cosmetics | No — they change *how* it is built, not *which* project |

A caveat from the top of that page that argues against an exhaustive-metadata design:

> MSBuild itself doesn't set any value for optional metadata, and unset metadata is equivalent to an
> empty string. … However, metadata values are sometimes set in SDK files that are implicitly
> imported.

and, on `ProjectReference` specifically:

> `ProjectReference` items are transformed into Reference items by the `ResolveProjectReferences`
> target, so any valid metadata on a Reference may be valid on `ProjectReference`

`AdditionalProperties` and `UndefineProperties` are **not** in that page's `ProjectReference` table —
they are documented as MSBuild-task `Projects` metadata
(<https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-task>) and, in the case of
`UndefineProperties`, only in source. They are irrelevant to graph construction.

---

## 3. Output locations

Everything in this section was read from the installed SDK 10.0.303 and cross-checked against the
public repositories. Line numbers refer to the local copies under
`C:\Program Files\dotnet\sdk\10.0.303\`.

### 3.1 The default layout

`Microsoft.Common.CurrentVersion.targets` lines 147-156:

```xml
<Platform Condition="'$(Platform)' == ''">AnyCPU</Platform>
<Configuration Condition="'$(Configuration)' == ''">Debug</Configuration>
<BaseOutputPath Condition="'$(BaseOutputPath)' == ''">bin\</BaseOutputPath>
<OutputPath Condition="'$(OutputPath)' == '' and '$(PlatformName)' == 'AnyCPU'">$(BaseOutputPath)$(Configuration)\</OutputPath>
<OutputPath Condition="'$(OutputPath)' == '' and '$(PlatformName)' != 'AnyCPU'">$(BaseOutputPath)$(PlatformName)\$(Configuration)\</OutputPath>
```

`Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.DefaultOutputPaths.targets` lines 24-28, 101-106 and
140-143:

```xml
<AppendTargetFrameworkToOutputPath Condition="'$(AppendTargetFrameworkToOutputPath)' == ''">true</AppendTargetFrameworkToOutputPath>
<AppendPlatformToOutputPath Condition="'$(AppendPlatformToOutputPath)' == '' and '$(PlatformName)' == 'AnyCPU'">false</AppendPlatformToOutputPath>
<AppendPlatformToOutputPath Condition="'$(AppendPlatformToOutputPath)' == '' and '$(PlatformName)' != 'AnyCPU'">true</AppendPlatformToOutputPath>
...
<OutputPath Condition="'$(OutputPath)' == ''">$(BaseOutputPath)$(_PlatformToAppendToOutputPath)$(Configuration)\</OutputPath>
...
<PropertyGroup Condition="'$(UseArtifactsOutput)' != 'true' and
                          '$(AppendTargetFrameworkToOutputPath)' == 'true' and '$(TargetFramework)' != '' ...">
  <OutputPath>$(OutputPath)$(TargetFramework.ToLowerInvariant())\</OutputPath>
</PropertyGroup>
```

and `Microsoft.NET.RuntimeIdentifierInference.targets` lines 390-403:

```xml
<AppendRuntimeIdentifierToOutputPath Condition="'$(AppendRuntimeIdentifierToOutputPath)' == ''">true</AppendRuntimeIdentifierToOutputPath>
...
<PropertyGroup Condition="'$(AppendRuntimeIdentifierToOutputPath)' == 'true' and '$(RuntimeIdentifier)' != '' and '$(_UsingDefaultRuntimeIdentifier)' != 'true'">
  <OutputPath Condition="'$(UseArtifactsOutput)' != 'true'">$(OutputPath)$(RuntimeIdentifier)\</OutputPath>
</PropertyGroup>
```

So the assembled default is:

```
bin\ [ {Platform}\ if not AnyCPU ] {Configuration}\ {tfm-lowercased}\ [ {rid}\ ]
```

Five details that will bite a naive implementation:

- **The TFM segment is lower-cased** (`$(TargetFramework.ToLowerInvariant())`). Matters for
  `net8.0-windows10.0.19041.0`-style monikers, and on case-sensitive file systems.
- **The RID segment is NOT lower-cased** — the RID append is a plain `$(OutputPath)$(RuntimeIdentifier)\`.
  So the two appended segments have different casing rules.
- **The Configuration segment is NOT lower-cased** — it is `Debug`/`Release` as written.
- **A non-`AnyCPU` platform inserts a segment before the configuration**: `bin\x64\Debug\net8.0\`.
  Which platform applies comes from the solution's per-project mapping, which is exactly what
  `SolutionProjectModel.GetProjectConfiguration` returns (§1.5).
- **A RID appends a segment**: `bin\Debug\net8.0\win-x64\`. `dotnet publish` writes to
  `$(OutputPath)publish\` (or `$(OutputPath)$(RuntimeIdentifier)\publish\`) —
  `Microsoft.NET.Sdk.BeforeCommon.targets` lines 129-137. Note `dotnet publish` defaults to
  `Release`, unlike `dotnet build`.

`dotnet build` defaults to `Debug`; `-c Release` sets `$(Configuration)`.

The assembly path itself is `Microsoft.Common.CurrentVersion.targets` lines 238-324:
`OutDir` defaults to `OutputPath`; `TargetDir` is `OutDir` made absolute against the project
directory; `TargetPath` is `$(TargetDir)$(TargetFileName)`. That is the value
`-getProperty:TargetPath` would return, and the thing Reach is trying to predict.

The two appends are documented at
<https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props>:

> The .NET SDK automatically appends the target framework and, if present, the runtime identifier to
> the output path. Setting `AppendTargetFrameworkToOutputPath` to `false` prevents the TFM from being
> appended to the output path. However, without the TFM in the output path, multiple build artifacts
> may overwrite each other.

> Setting `AppendRuntimeIdentifierToOutputPath` to `false` prevents the RID from being appended to
> the output path. (However, the RID **is** still appended to the publish path.)

### 3.2 Artifacts output

Opt-in, and **the layout is genuinely different** — not just a different root.
Documented at <https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output>:

> In .NET 8 and later versions, there's an option to simplify the output path and folder structure
> for build outputs.

> By default, the common location is a directory named *artifacts* next to the
> *Directory.build.props* file.

```
📁 artifacts
    └──📂 <Type of output>        bin | obj | publish | package
        └──📂 <Project name>      "Defaults to the MSBuild project name"
            └──📂 <Pivot>         debug | debug_net8.0 | release | release_linux-x64
```

> Pivot | Distinguishes between builds of a project for different configurations, target frameworks,
> and runtime identifiers. If multiple elements are needed, they're joined by an underscore (`_`).

The doc's own examples table confirms the multi-targeting rule that matters most:
*artifacts\bin\MyApp\debug* is "the build output path for a **simple** project", while
*artifacts\bin\MyApp\debug_net8.0* is "the build output path for the `net8.0` build of a
**multi-targeted** project". (The rendered page shows that second path as `debug\_net8.0`, which is
a markdown-escaping artefact — the source proves it is `debug_net8.0`, one segment.)
`Microsoft.NET.DefaultOutputPaths.targets` lines 48-95:

```xml
<ArtifactsProjectName Condition="'$(ArtifactsProjectName)' == ''">$(MSBuildProjectName)</ArtifactsProjectName>
<ArtifactsBinOutputName Condition="'$(ArtifactsBinOutputName)' == ''">bin</ArtifactsBinOutputName>
...
<ArtifactsPivots>$(Configuration.ToLowerInvariant())</ArtifactsPivots>
<ArtifactsPivots Condition="'$(TargetFrameworks)' != '' And '$(TargetFramework)' != ''"
                 >$(ArtifactsPivots)_$(TargetFramework.ToLowerInvariant())</ArtifactsPivots>
<ArtifactsPivots Condition="'$(RuntimeIdentifier)' != '' And ..."
                 >$(ArtifactsPivots)_$(RuntimeIdentifier.ToLowerInvariant())</ArtifactsPivots>
...
<BaseOutputPath Condition="'$(BaseOutputPath)' == ''">$(ArtifactsPath)\$(ArtifactsBinOutputName)\$(ArtifactsProjectName)\</BaseOutputPath>
<OutputPath Condition="'$(OutputPath)' == ''">$(BaseOutputPath)$(ArtifactsPivots)\</OutputPath>
```

Giving:

```
{ArtifactsPath}\bin\{MSBuildProjectName}\{configuration-lowercased}[_{tfm-lowercased}][_{rid-lowercased}]\
```

**Three things about this that are easy to get wrong:**

1. **The project segment is `$(MSBuildProjectName)` — the project file name — not `AssemblyName`.**
   A project whose `AssemblyName` differs from its file name still lands under the file name.
2. **A single-targeted project gets NO TFM segment.** The TFM pivot is appended only when
   `$(TargetFrameworks)` (plural) is non-empty. So `artifacts\bin\Lib\debug\` for a single-TFM
   project, `artifacts\bin\Lib\debug_net8.0\` for a multi-targeted one. A discovery routine that
   assumes a TFM directory will find nothing.
3. **Configuration is lower-cased here**, unlike the default layout. Separator between pivots is
   `_`, not a directory boundary.

`IncludeProjectNameInArtifactsPaths` can be set `false`, which drops the project segment entirely
and puts every project's output in the same directory — a case Reach must at least detect, because
assembly-to-project attribution by directory then fails.

**Where `ArtifactsPath` defaults to** (`Microsoft.NET.DefaultArtifactsPath.props` lines 23-42):
`$(_DirectoryBuildPropsBasePath)\artifacts` — i.e. **beside the nearest `Directory.Build.props`**,
not beside the solution. Setting `ArtifactsPath` alone implies `UseArtifactsOutput=true`.

**It must be set in `Directory.Build.props`** (or on the command line), and the SDK **errors**
otherwise — this is a hard error, not a warning. `Microsoft.NET.DefaultOutputPaths.targets`
lines 151-167:

```xml
<NetSdkError Condition="'$(UseArtifactsOutput)' == 'true' and '$(_ArtifactsPathSetEarly)' != 'true'"
             ResourceName="ArtifactsPathCannotBeSetInProject" />
<NetSdkError Condition="'$(_ArtifactsPathLocationType)' == 'ProjectFolder'"
             ResourceName="UseArtifactsOutputRequiresDirectoryBuildProps" />
```

The message texts, from the SDK's `Strings.resx`:

> **NETSDK1199**: The ArtifactsPath and UseArtifactsOutput properties cannot be set in a project
> file, due to MSBuild ordering constraints. They must be set in a Directory.Build.props file or
> from the command line.

> **NETSDK1200**: If UseArtifactsPath is set to true and ArtifactsPath is not set, there must be a
> Directory.Build.props file in order to determine where the artifacts folder should be located.

`_ArtifactsPathSetEarly` is stamped in `UseArtifactsOutputPath.props`, imported from `Sdk.props`
before the project body — which is why a command-line global property satisfies it and a project-file
setting does not.

**Good news for Reach**: because the SDK itself forces the setting into `Directory.Build.props`,
Reach can detect artifacts output by walking up from the project for `Directory.Build.props` and
looking for `UseArtifactsOutput` or `ArtifactsPath` — no evaluation required. The one gap is
`-p:UseArtifactsOutput=true` / `--artifacts-path` passed on the command line
(`dotnet build --artifacts-path <dir>` is a documented option, **verified here** in
`dotnet build --help` on SDK 10.0.303), which Reach cannot see unless it is Reach that invokes
the build.

**Scanner hazard: the outer build of a multi-targeted project has a real, empty output directory.**
The outer (cross-targeting) pass evaluates with `$(TargetFramework)` empty, so its `ArtifactsPivots`
is just `debug` and its `OutputPath` is `artifacts\bin\Multi\debug\` — a genuine computed path that
contains no files, while the assemblies live in `debug_net8.0\` and `debug_net9.0\`. Reach must not
read the empty directory as a build failure.

The SDK deliberately does **not** default this on; the targets carry a commented-out block showing
where they *would* enable it for `net8.0`+.

One documentation trap: the design document behind `aka.ms/netsdk1199` still calls the folder
`.artifacts` with a leading dot. The shipped implementation uses `artifacts`. Cite the targets file.

### 3.3 Overrides

Any project may set `OutputPath` or `BaseOutputPath` to an arbitrary relative or absolute path, and
that wins — every default above is `Condition="'$(OutputPath)' == ''"`. It can be set in the project,
in `Directory.Build.props`, or as a global property. In the local sample (§2.2), 11 of 420 SDK-style
projects set one, and all 50 legacy projects did. The header comment in
`Microsoft.Common.CurrentVersion.targets` states the precedence:

> **OutputPath**: This is the full Output Path, and is derived from BaseOutputPath, if none
> specified (eg. bin\Debug). **If this property is overridden, then setting BaseOutputPath has no
> effect.**

**Trap 1 — a project-file `<OutputPath>` still gets the TFM and RID appended.** The append blocks
(§3.1) are *not* conditioned on `OutputPath` having been defaulted. So
`<OutputPath>custom\out\</OutputPath>` in the csproj yields `custom\out\net8.0\`, and with a RID,
`custom\out\net8.0\win-x64\`. Confirmed by `-getProperty` on SDK 10.0.303.

**Trap 2 — the same value passed as a global property does NOT get them appended**, because MSBuild
global properties are immutable during evaluation and the append is a plain assignment. So:

| How `OutputPath` was set | Resulting directory |
|---|---|
| `<OutputPath>X</OutputPath>` in the project | `X\<tfm>\[<rid>\]` |
| `-p:OutputPath=X`, or `dotnet build -o X` | `X\` exactly — no TFM, no RID |

**This is the single most important fact for a tool that predicts output locations statically: the
project file alone cannot tell you whether a TFM segment is present.**

**`OutputPath` alongside artifacts output is a silent split-brain.** With `UseArtifactsOutput=true`
in `Directory.Build.props` and `<OutputPath>myout\</OutputPath>` in the project, the binaries go to
`myout\` — with **no** TFM appended, because that append is gated on `UseArtifactsOutput != true` —
while the intermediates go to `artifacts\obj\<Project>\debug\`, and the build reports zero warnings.
An exhaustive grep of the SDK's message catalogue found only NETSDK1199 and NETSDK1200 mentioning
artifacts; **no diagnostic covers this combination.** (A negative result from an exhaustive grep
plus a clean build, not a documented statement.)

`dotnet build -o|--output <dir>` also overrides, per
<https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build>. What it actually does is forward
**two MSBuild global properties**
([OptionExtensions.cs](https://raw.githubusercontent.com/dotnet/sdk/main/src/Cli/Microsoft.DotNet.Cli.Definitions/Utilities/OptionExtensions.cs)):

```csharp
return [
    $"--property:{outputPropertyName}={argVal}",
    "--property:_CommandLineDefinedOutputPath=true"
];
```

`dotnet build` wires it as `ForwardAsOutputPath("OutputPath")`, `dotnet publish` as
`ForwardAsOutputPath("PublishDir")`. `--artifacts-path` forwards a single
`--property:ArtifactsPath=…` with no companion.

Four consequences for a scanner:

- **`-o` is Trap 2.** It arrives as a global property, so no TFM and no RID segment — a flat
  directory. Confirmed by `dotnet msbuild -getProperty:OutputPath` (evaluation only, no build) on
  SDK 10.0.303.
- **A relative `-o` is anchored to the CLI's working directory, not the project directory.** The
  value is run through `CommandDirectoryContext.GetFullPath` and forwarded absolute. So
  `dotnet build src/App/App.csproj -o out` writes to `./out`, not `src/App/out`.
- **On a solution, `-o` collapses every project's output into one directory**, and it is a
  *warning*, not an error, so the build succeeds and the damage is done.
  <https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build>:

  > If you specify the `--output` option when running this command on a solution, the CLI will emit
  > a warning (an error in 7.0.200) due to the unclear semantics of the output path. The `--output`
  > option is disallowed because all outputs of all built projects would be copied into the
  > specified directory, which isn't compatible with multi-targeted projects…

  The diagnostic is **NETSDK1194**: *"The "--output" option isn't supported when building a
  solution. Specifying a solution-level output path results in all projects copying outputs to the
  same directory, which can lead to inconsistent builds."* The severity history is documented at
  <https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/7.0/solution-level-output-no-longer-valid>
  — an error in 7.0.200, *"reduced to a warning level of severity in the 7.0.201 SDK"*. Confirmed on
  SDK 10.0.303: `dotnet build S.slnx -o out` warns and still succeeds.

  `_CommandLineDefinedOutputPath` exists solely to trigger this warning and has no effect on path
  computation. Local SDK, `Current\SolutionFile\ImportAfter\Microsoft.NET.Sdk.Solution.targets`
  lines 32-38:

  ```xml
  <Target Name="_CheckForSolutionLevelOutputPath"
          BeforeTargets="Build;Publish;Clean;Store;VSTest;_MTPBuild"
          Condition="'$(_CommandLineDefinedOutputPath)' == 'true'">
    <NetSdkWarning ResourceName="CannotHaveSolutionLevelOutputPath" />
  </Target>
  ```

  That file sits under `SolutionFile\ImportAfter\`, so it is imported only when building a
  `.sln`/`.slnx`; the property is inert for a direct project build.
- **`-o` on a multi-targeted project produces a directory of indeterminate TFM provenance.** The
  docs say *"For projects with multiple target frameworks … you also need to define `--framework`
  when you specify this option"*, but on SDK 10.0.303 `dotnet build M.csproj -o out2` with
  `TargetFrameworks=net8.0;net9.0` succeeded with zero warnings and wrote **one** set of
  `M.dll`/`M.pdb` — both inner builds raced to the same directory and the last writer won. No doc
  or issue explaining that discrepancy was found.

**`--artifacts-path` must be cascaded to every downstream command.** From the CLI docs:

> This option and the value provided must be explicitly cascaded in any dotnet command that depends
> on the output of another dotnet command, for example, when using `dotnet build --no-restore` and
> `dotnet publish --no-build`. Available since .NET 8 SDK.

That is exactly Reach's `--no-build` situation: if the pipeline's build step used
`--artifacts-path X`, Reach must be told `X` too, or it will scan the wrong tree and conclude every
assembly is missing.

**Reach should refuse `--no-build` against a tree built with a solution-level `-o`**, or at least
warn loudly, because one flat directory makes assembly-to-project attribution impossible and
therefore makes analysis scope unverifiable — which ADR-0002 says must be an error.

### 3.4 What Reach can detect, and what must be an input

| Fact | How Reach gets it | Confidence |
|---|---|---|
| Project list, project type, per-configuration build flag | Solution file, via `SolutionPersistence` (§1) | High — no evaluation |
| Project graph edges | `ProjectReference` from raw XML (§2) | High for the common case; **must fall back loudly** |
| Configuration (`Debug`/`Release`) | **Command-line input, default `Debug`** | Cannot be inferred; the SDK default is `Debug` and a CI pipeline usually uses `Release` |
| Platform | Solution's per-project mapping via `GetProjectConfiguration`, defaulting to `AnyCPU` | Medium |
| Target frameworks | `<TargetFramework>` / `<TargetFrameworks>` from the project XML | Medium — can come from `Directory.Build.props` (the local sample found 4 of 17 `Directory.Build.props` files setting `TargetFramework`) or be property-parameterised |
| `UseArtifactsOutput` / `ArtifactsPath` | Walk up for `Directory.Build.props` and read it; the SDK guarantees it is there | Medium-high |
| `OutputPath` / `BaseOutputPath` override | Read from project XML and `Directory.Build.props` | Medium — invisible if set by an imported file or `-p:`, and §3.3's Trap 1/Trap 2 mean the *same value* produces different layouts depending on where it was set |
| RID | `<RuntimeIdentifier>` / `<RuntimeIdentifiers>` from XML, or the command line | Low-medium |
| Whether the build used `-o` or `--artifacts-path` | **Command-line input only.** Nothing on disk records it | None — must be told, or Reach must own the build |
| The actual assembly on disk | **Enumerate the directory** | The only high-confidence answer |

**Recommended design.** Treat path prediction as a *search hint*, not an oracle:

1. Predict candidate output directories per (project, TFM) from the rules above — plural, because
   Trap 1/Trap 2 make several layouts equally plausible from static evidence.
2. Enumerate them and match assemblies by discovery, not by predicted file name.
3. If a project's expected assembly is not found, **error** — the map already says a missing
   assembly is an error, and ADR-0002 says Reach must not answer narrowly when scope cannot be
   established.
4. Expose an explicit escape hatch (`--output-root` / `--configuration` / `--artifacts-path`) so an
   unusual layout is a one-flag fix rather than a wall. `--artifacts-path` in particular is not
   optional politeness: the CLI docs require it to be cascaded to any command consuming another's
   output, which is precisely `--no-build`.

**This strengthens the case for Reach owning the build by default** (PRD §12's default mode). When
Reach invokes `dotnet build` it knows the configuration, the artifacts path and whether `-o` was
used, because it chose them. Under `--no-build` none of that is recoverable from disk — which is
another entry for PRD §11's open question about whether `--no-build` should ship at all.

**The evaluation-based escape hatch, for the record.**
<https://learn.microsoft.com/en-us/visualstudio/msbuild/evaluate-items-and-properties>:

> The following command-line options are available in **MSBuild 17.8 and later**.
> `-getProperty:{propertyName,...}` Get the value of the specified property or properties.
> `-getItem:{itemName,...}` … `-getTargetResult:{targetName,...}` …

> If you don't specify a target on the command line by using the `-target` option, then the
> `-getProperty` and `-getItem` options return the values from MSBuild evaluation, and **no targets
> are built**.

> If you use `-getProperty` to request a single property, the output is emitted as a string of text
> … If you use `-getProperty` to request multiple properties, or use `-getItem` or
> `-getTargetResult`, the output is in a **JSON** format.

> You can use these commands with MSBuild.exe or with `dotnet build`, or other `dotnet` commands.

So `dotnet msbuild <proj> -getProperty:TargetPath` returns the exact assembly path, with every trap
in §3.1-§3.3 already resolved, and compiles nothing.

This does not contradict PRD §9.2, which rejected `MSBuildWorkspace.OpenSolutionAsync` — loading a
whole solution through Roslyn — not evaluating one project. But it is a **full MSBuild evaluation**:
SDK resolution, every import, `Directory.Build.props`/`.targets`, and NuGet's generated props and
targets from `obj/` if restore has run. Roughly 1-2 s per project per invocation on this machine,
and one process per project unless driven through a traversal project. **This figure comes from a
delegated run, not a controlled measurement**, and a real solution was never timed.

Recommendation: keep it as an opt-in "accurate mode" for when directory scanning is ambiguous, not
as the default path. If predict-then-discover proves brittle, timing `-getProperty` on a real
solution is the obvious next spike.

---

## 4. Debug symbols: where they land

Scope reminder: **location only**. Nothing here touches PDB contents, and nothing here bears on
ADR-0003's checksum claim.

### 4.1 Default is a portable PDB, in both configurations

<https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/code-generation>:

> The **DebugType** option causes the compiler to generate debugging information and place it in the
> output file or files. **The default value is `portable` for both Debug and Release build
> configurations, which means PDB files are generated by default for all configurations.**

| `DebugType` | Meaning (verbatim) |
|---|---|
| `full` | "Emit debugging information to *.pdb* file using default format for the current platform: **Windows**: A Windows pdb file. **Linux/macOS**: A Portable PDB file." |
| `pdbonly` | "Same as `full`." |
| `portable` | "Emit debugging information to .pdb file using cross-platform Portable PDB format." |
| `embedded` | "**Emit debugging information into the *.dll/.exe* itself (*.pdb* file is not produced)** using Portable PDB format." |
| `none` | "Don't produce a PDB file." |

Confirmed in the installed SDK. `Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.Sdk.props`
lines 96-97:

```xml
<DebugType Condition="'$(DebugSymbols)' == 'false'">None</DebugType>
<DebugType Condition=" '$(DebugType)' == '' ">portable</DebugType>
```

### 4.2 The single switch that decides whether a `.pdb` exists on disk

`Microsoft.Common.CurrentVersion.targets` lines 174-181 — this is the authoritative predicate:

```xml
<_DebugSymbolsProduced>false</_DebugSymbolsProduced>
<_DebugSymbolsProduced Condition="'$(DebugSymbols)'=='true'">true</_DebugSymbolsProduced>
<_DebugSymbolsProduced Condition="'$(DebugType)'=='none'">false</_DebugSymbolsProduced>
<_DebugSymbolsProduced Condition="'$(DebugType)'=='pdbonly'">true</_DebugSymbolsProduced>
<_DebugSymbolsProduced Condition="'$(DebugType)'=='full'">true</_DebugSymbolsProduced>
<_DebugSymbolsProduced Condition="'$(DebugType)'=='portable'">true</_DebugSymbolsProduced>
<_DebugSymbolsProduced Condition="'$(DebugType)'=='embedded'">false</_DebugSymbolsProduced>
<_DebugSymbolsProduced Condition="'$(ProduceOnlyReferenceAssembly)'=='true'">false</_DebugSymbolsProduced>
```

So a separate `.pdb` file is absent in exactly four cases: `DebugType=none`, `DebugType=embedded`,
`ProduceOnlyReferenceAssembly=true`, or `DebugSymbols=false` (which the SDK turns into
`DebugType=None`).

**Do not read `$(DebugSymbols)` to predict PDB presence — read `$(DebugType)`.** In a Release build
`DebugSymbols` evaluates to `false`, yet `_DebugSymbolsProduced` is `true`, because the
`DebugType=='portable'` clause is evaluated *after* the `DebugSymbols` clause and overwrites it.
**Release builds do produce PDBs by default.**

### 4.3 Where the file lands

`Microsoft.Common.CurrentVersion.targets` lines 409-413:

```xml
<ItemGroup Condition="'$(_DebugSymbolsProduced)' == 'true'">
  <_DebugSymbolsIntermediatePath Include="$(IntermediateOutputPath)$(TargetName).pdb" Condition="'$(OutputType)' != 'winmdobj' and '@(_DebugSymbolsIntermediatePath)' == ''"/>
  <_DebugSymbolsOutputPath Include="@(_DebugSymbolsIntermediatePath->'$(OutDir)%(Filename)%(Extension)')" />
</ItemGroup>
```

and the copy, line 4986, gated on two further opt-outs (line 4996):

```xml
<Copy SourceFiles="@(_DebugSymbolsIntermediatePath)" DestinationFiles="@(_DebugSymbolsOutputPath)" ...
      Condition="'$(_DebugSymbolsProduced)'=='true' and '$(SkipCopyingSymbolsToOutputDirectory)' != 'true' and '$(CopyOutputSymbolsToOutputDirectory)'=='true'">
```

**Net rule: `$(OutDir)$(TargetName).pdb` — same directory as the assembly, same base name.**
`TargetName` defaults to `AssemblyName` and `TargetFileName` is `$(TargetName)$(TargetExt)` (§2.6),
so `Foo.pdb` sits beside `Foo.dll`. Note the base name is `AssemblyName`, **not** the project file
name: `Foo.csproj` with `<AssemblyName>Bar</AssemblyName>` produces `Bar.dll` + `Bar.pdb`.
`$(OutDir)` follows `$(OutputPath)`, so the artifacts layout (§3.2) moves the PDB along with the
assembly and nothing special is needed.

The compiler docs say the same
(<https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/advanced#pdbfile>):

> When you specify DebugType, the compiler creates a *.pdb* file **in the same directory where the
> compiler creates the output file** (.exe or .dll). The *.pdb* file has **the same base file name**
> as the name of the output file. **PdbFile** allows you to specify a nondefault file name and
> location for the *.pdb* file.

Three ways the file can move or vanish even when it is produced:

- `SkipCopyingSymbolsToOutputDirectory=true` or `CopyOutputSymbolsToOutputDirectory=false` — the PDB
  stays in `obj\` and never reaches the output directory.
- `<PdbFile>` (compiler `-pdb`) — the one supported way a PDB legitimately sits somewhere other than
  beside its assembly. It is passed straight to the compiler task
  (`Roslyn\Microsoft.CSharp.Core.targets` line 147), and the SDK itself uses it for `winmdobj`.
- `winmdobj` output uses `$(WinMDExpOutputPdb)` and a separate copy path (lines 415-418).

**`dotnet publish` does copy the PDB by default.** `Microsoft.NET.Publish.targets` sets
`CopyOutputSymbolsToPublishDirectory` to `true` and adds `@(_DebugSymbolsIntermediatePath)` to
`ResolvedFileToPublish`. The exception is `PublishSingleFile`, where `IncludeSymbolsInSingleFile`
defaults to `false` so the PDB stays outside the bundle.

### 4.4 What a PDB beside a DLL does and does not prove

**A PDB in the output folder proves nothing about who produced the assembly.** `.pdb` is a
*reference-related file extension*, so the reference resolver copies the PDB of every copy-local
reference into the **consuming** project's output directory.
`Microsoft.Common.CurrentVersion.targets`:

```xml
<!-- These are the extensions that reference resolution will consider when looking for files related to resolved references. -->
<AllowedReferenceRelatedFileExtensions Condition=" '$(AllowedReferenceRelatedFileExtensions)' == '' ">
  .pdb;
  .xml;
  .pri;
  .dll.config;
  .exe.config
</AllowedReferenceRelatedFileExtensions>
```

So a test project's `bin\Debug\net8.0\` contains PDBs for every project it references, and for any
package that ships one in `lib/`.

A **standard NuGet package does not** ship PDBs, though. NuGet's pack targets exclude `.pdb` from
the main package and allow it only in the symbols package:

```xml
<DefaultAllowedOutputExtensionsInPackageBuildOutputFolder>.dll; .exe; .winmd; .json; .pri; .xml</DefaultAllowedOutputExtensionsInPackageBuildOutputFolder>
<AllowedOutputExtensionsInSymbolsPackageBuildOutputFolder Condition="'$(SymbolPackageFormat)' == 'snupkg'">.pdb</AllowedOutputExtensionsInSymbolsPackageBuildOutputFolder>
```

and <https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#includesymbols> confirms
`IncludeSymbols=true` *"creates a regular package **and** a symbols package"*. `.snupkg` /
`.symbols.nupkg` are separate files that `dotnet restore` does not fetch, so their PDBs never reach
the packages folder or the output directory. A package author *can* put a PDB in `lib/`, but it is
not what packing does by default.

`DebugType=embedded` is the opposite hazard: a first-party assembly with **no** `.pdb` beside it.
Reach must read symbols from the PE in that case rather than concluding the assembly is not
first-party — treating a missing `.pdb` as "not ours" would silently drop code from analysis scope,
which is under-selection. Embedded symbols are common in packaged libraries, so absence proves as
little as presence.

`DebugType=none` / `DebugSymbols=false` leaves no symbols at all, in the PE or beside it. The
`First-party assembly` definition then has nothing to work with, so this must be a loud error or a
whole-project selection, never a silent exclusion.

**Rule for Reach: PDB presence is a hint, never an identity or provenance signal. Key off the
assembly.**

### 4.5 SourceLink and determinism do not move the PDB

No primary source claims Source Link or deterministic builds change where a PDB is written, and no
mechanism was found by which they could.
<https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink> describes embedding
source-control metadata *inside* assemblies and packages; its only location-adjacent sentence is
about package layout — *"Verify that the Repository metadata is present with a commit identifier and
that .pdb files are located with each target's .dll."* The determinism knobs (`Deterministic`,
`ContinuousIntegrationBuild`, `PathMap`, `DeterministicSourcePaths`) change paths recorded *inside*
the PDB, which is content, not placement. **Treat "the PDB is beside the assembly" as safe
regardless of Source Link.**

Per the ticket, PDB *contents* — including source-file checksums — were not investigated.

---

## 5. Things this note could not establish

Stated explicitly so nobody treats silence as agreement.

1. **How often property-parameterised or conditioned `ProjectReference` paths occur in real
   codebases.** No first-party source exists. The local 40-repository sample found zero, but it is
   one machine and not a corpus.
2. **Whether `ProjectReference Include` supports a directory, or is expected to support globs.**
   Undocumented either way; the targets' `Exists('%(Identity)')` check would pass a directory and
   then fail inside the MSBuild task, but this was not tested.
3. **The real cost of `dotnet msbuild -getProperty:…` on a solution-sized workload.** A delegated
   run suggested roughly 1-2 s per project invocation on this machine, but that was not a controlled
   measurement and no real solution was timed.
4. **Whether `.slnx` ever sat behind a Visual Studio preview flag.** The widely-repeated claim
   ("Use Solution File Persistence Model" under Environment → Preview Features, GA in 17.14) could
   not be corroborated on any primary source. VS 17.13 and 17.14 release notes contain zero mentions
   of `slnx`, and the main VS solutions concept page is stale — it still describes only `.sln`/`.suo`.
5. **Why `dotnet build -o` on a multi-targeted project emits no warning** on SDK 10.0.303, despite
   the docs saying `--framework` is also required. The inner builds race to one directory and the
   last writer wins. No doc or issue explaining the discrepancy was found.
6. **Anything at all about PDB contents**, including source-file checksums — deliberately out of
   scope per the ticket.

Two negative results that are conclusions from exhaustive search rather than documented statements,
and should be re-checked if anything depends on them:

- **No SDK diagnostic exists for `OutputPath` set alongside artifacts output** (§3.3). Basis: a grep
  of the SDK's complete `Strings.resx` message catalogue for "Artifact" returning only NETSDK1199
  and NETSDK1200, plus a build producing zero warnings.
- **Nothing documents Source Link or determinism affecting PDB location** (§4.5).

Three documentation defects worth knowing when re-checking any of this:

- `msbuild-props#debugtype` is a **dead anchor** — `DebugType` is not documented on
  <https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props> at all. That page says
  of itself: *"This page is a work in progress and does not list all of the useful MSBuild
  properties for the .NET SDK."* Cite the C# compiler-options page instead.
- The artifacts-output examples table renders `debug\_net8.0`; the real path is `debug_net8.0`
  (§3.2), and the design doc behind `aka.ms/netsdk1199` still says `.artifacts` with a leading dot.
- `dotnet sln`'s documented *"the user is prompted to specify a file explicitly"* when several
  solutions are found is inaccurate — the CLI throws and exits non-zero.

---

## 6. Method

- **Local SDK inspection.** Targets and props read from `C:\Program Files\dotnet\sdk\10.0.303\`,
  which is the shipping copy of `dotnet/msbuild` and `dotnet/sdk` sources cited above.
- **Assembly inspection.** `Microsoft.VisualStudio.SolutionPersistence.dll` 1.0.52 loaded by
  reflection to enumerate exported types, members, referenced assemblies and attributes.
- **Library behaviour.** The same assembly driven directly through reflection from PowerShell
  against hand-written `.sln` and `.slnx` fixtures in a scratch directory. No project was compiled
  and nothing in this repository was built or modified.
- **CLI behaviour.** Read-only `dotnet sln … list`, `dotnet sln --help`, `dotnet build --help`,
  `dotnet --list-sdks`. Nothing in this repository was built or modified at any point.
- **Delegated verification.** Parts of §3 and §4 were cross-checked by a delegated agent running
  `dotnet msbuild -getProperty` (evaluation only) and throwaway builds in a scratch directory on
  this machine, against SDK 10.0.303. Claims resting on that rather than on my own runs are marked
  in place: the Trap 1 / Trap 2 layouts, the artifacts pivot cases, the `OutputPath`-plus-artifacts
  split-brain, NETSDK1194's warning severity, the multi-targeted `-o` race, and the `-getProperty`
  timing.
- **Corpus sample.** Read-only scan of `*.csproj` under `C:\Dev`, excluding
  `bin`/`obj`/`node_modules`/`packages`/`artifacts`.
- **Documentation.** learn.microsoft.com pages and raw files from `dotnet/sdk`, `dotnet/msbuild`,
  `dotnet/roslyn`, `dotnet/runtime` and `microsoft/vs-solutionpersistence`, plus the nuget.org V3
  API and the unpacked `.nupkg`.
