# 14: Deterministic source paths map to the repository root

**What to build:** A CI build with `ContinuousIntegrationBuild=true` records every source document as `/_/src/...` instead of its real path (the SDK's `DeterministicSourcePaths`). Reach then reads every first-party assembly as "its debug symbols point at source outside the working tree" and the run fails. Reach maps the deterministic prefix back to the repository root where PDB document names become paths, so a CI-built solution is recognised as first-party and verified. See [the spec](../spec.md), ticket 13's `## Comments` and ADR-0003.

Maintainer decision (from ticket 13's dogfood findings): "Teach Reach to map to repo root. This is likely to happen anywhere where Reach is used and reproducible builds are enabled."

- Research exactly how `SourceRoot` becomes `PathMap` (`/_/`, `/_1/`, ...), including submodules, and whether the PDB records the mapping.
- Map `/_/` to the git top-level in `PdbDocuments.PathOf`, the one place a PDB document name becomes a path. Correspondence, first-party classification, the tier ladder and the span join then all see real paths.
- Handle further roots (`/_1/`, `/_2/`, ...) on principle: map them only if they can be determined; otherwise do not guess, and say so in a notice.
- Remove the test-side `/_/` mapping in `PortablePdbChecksumTests` (commit c35e1e6) if the product now covers it.
- Update `docs/limitations.md` and the docs as needed.

**Blocked by:** None

**Status:** ready-for-agent

- [x] An assembly compiled with a `/_/` path map is first-party and its documents resolve to files in the working tree (in-memory compilation with a path map)
- [x] Correspondence verifies such an assembly, and the span join matches its sequence points
- [x] A document under a further root (`/_1/`, ...) is not guessed at; the run says so in a notice
- [x] `PortablePdbChecksumTests` no longer maps `/_/` itself
- [x] Dogfood: this repository built with `GITHUB_ACTIONS=true` and run through `reach select` recognises its first-party assemblies
- [x] Build and format gates remain clean; all tests pass

## Comments

### What the SDK does (research)

Sources: `Microsoft.Managed.Core.targets` and `MapSourceRoots.cs` (dotnet/roslyn), `GitOperations.GetSourceRoots` (dotnet/sourcelink), Roslyn's `pdb-compilation-options.md`, and this repository's own CI-built PDBs.

- `DeterministicSourcePaths` defaults on when `Deterministic` and `ContinuousIntegrationBuild` are both true. `_SetPathMapFromSourceRoots` builds `PathMap` from the top-level `SourceRoot` items only.
- `MapSourceRoots` numbers top-level roots `"/_" + (index == 0 ? "" : index) + "/"`, source-controlled roots first. A nested root gets its containing root's prefix plus its relative path, and no number of its own.
- `LocateRepository` emits the git working directory as the one source-controlled top-level root. Each initialised submodule is a nested root under it. So `/_/` is always the git top-level, and submodules are `/_/<path>/`, never `/_1/`.
- `/_1/`, `/_2/`, ... are other top-level roots. Restore writes every NuGet package folder into `obj/*.nuget.g.props` as a `SourceRoot`, so `/_1/` is normally the global packages folder. This was found in the dogfood run: `Reach.Tests.dll` records `/_1/xunit.v3.core.mtp-v2/4.0.0/_content/DefaultRunnerReporters.cs`. A build can add its own roots, and static items come before the git root in `PathMap`, so the numbering is not reliable.
- Nothing in the PDB says which local directory a prefix stood for. SourceLink maps `/_/*` to a raw.githubusercontent URL. The compilation-options record leaves `pathmap` out by design. Checked on the real PDB.

### Design

- `PdbDocuments.PathOf` (still the only reader of a document name) delegates to `DeterministicSourcePaths.Map` with `SourceRoots(Repository, Packages)`. `/_/` maps to the git top-level. A document under a further root maps to the global packages folder (`NUGET_PACKAGES`, else `~/.nuget/packages`) only if the file exists there at the same relative path. Correspondence then checks its checksum. Anything else stays unmapped and carries `SourceDocument.UnmappedRoot`.
- Unmapped roots raise a new blind-spot notice, `unmapped-deterministic-source-root`, with a register entry and a schema row. Discovery names the cause when an assembly has nothing but unmapped documents.
- The span join caches each document's path per assembly, because mapping can now check the disk.
- ADR-0018 records the convention and the rejected alternatives: searching the working tree for the root, and passing `DeterministicSourcePaths=false` to the build.

### Verification

- Windows, normal build: 534/534, `-warnaserror` clean, `dotnet format --verify-no-changes` clean.
- Windows, `GITHUB_ACTIONS=true CI=true` Release build: 534/534 with `--no-build`. `PortablePdbChecksumTests` now reads Reach.Core against the repository root, and the product does the mapping.
- Dogfood (CI-condition Release build, `reach select -c Release --no-build --base HEAD~3`): every first-party assembly is recognised, correspondence is `verified`, and the run exits 0 with no `unmapped-deterministic-source-root` notice. One step still needs a hand-built `Contoso.Core` for net8.0, because of the inherited-`TargetFramework` problem that ticket 16 owns. Before this change, the same run reported every assembly as having source outside the working tree.
- Linux (`mcr.microsoft.com/dotnet/sdk:10.0`, fresh clone of the branch, `GITHUB_ACTIONS=true CI=true HUSKY=0`): `-warnaserror` build clean, format verification reported nothing, 534/534.
