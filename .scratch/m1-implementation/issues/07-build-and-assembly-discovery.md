# Build invocation and assembly discovery

Status: resolved
Depends on: 02, 04, 05
Spec: [§5](../../walking-skeleton/spec.md#5-build-handling), [§6.2](../../walking-skeleton/spec.md#62-assembly-discovery-scan-and-verify)–[§6.4](../../walking-skeleton/spec.md#64-stale-output)

## Goal

Compiled output on disk, and one file matched to each expected **assembly instance** — without
predicting a single path.

## Scope

**Build handling.** Default mode invokes `dotnet build` through `IProcessRunner`, forwarding
the `--` passthrough. On an already-built tree this is a fast no-op, because MSBuild's own
up-to-date checking decides; **Reach implements no up-to-date check of its own**. `--no-build`
skips it. **If the build fails, exit 2, select nothing, run nothing** — there is no degraded
mode, because a failed build already fails the pipeline and a clever selection over a broken
tree has no consumer.

**Discovery does not predict layouts. It scans and verifies.** Layout is undecidable from disk:
the same `OutputPath` produces `custom\out\net10.0\` when set in the project file and
`custom\out\` flat when forwarded as an MSBuild global property — which is what `dotnet build -o`
does, because global properties cannot be reassigned during evaluation — and **nothing on disk
records which happened**. Artifacts output is a third layout, in which a single-targeted project
gets *no* TFM segment. Predicting means enumerating every way MSBuild can be told where to put
output and hoping the list is complete.

The algorithm:

1. **Enumerate** `*.dll` under the target's directory tree — the solution's directory, or the
   project's for a single-project target — excluding `obj/`, `.git/`, `node_modules/`, `.vs/`
   and `packages/`.
2. **Prune by file name** against the expected assembly simple names from ticket 04. This is
   the cheap filter and it runs **before anything is opened**, which is what keeps a scan over
   a large repository affordable.
3. **Open** each survivor's metadata and debug symbols. **First-party iff the symbols point at
   source inside the working tree.** Symbols sitting *beside* an assembly prove nothing — `.pdb`
   is an `AllowedReferenceRelatedFileExtension`, so dependencies' symbols are copied into
   consuming projects' output — so classification must resolve the document paths recorded
   *inside* the symbols, never check whether a file exists beside the assembly.
4. **Read `TargetFrameworkAttribute` and `TargetPlatformAttribute`** from the assembly itself,
   never from a path segment. This is the payoff: the TFM is exactly what every ambiguous layout
   destroys on disk.
5. **Match** to the expected `(project, target framework)` set.

Every ambiguous case then falls out rather than needing a rule: `OutputPath` in the project file
versus as a global property, artifacts output's missing TFM segment, relative `-o` absolutised
against the CLI's working directory, and the multi-targeted outer build's computed-but-empty
directory — which cannot read as a missing assembly because **no path is ever computed**.

**Errors.** Scan-and-verify makes absence unambiguous: the tree was searched.

- **A missing expected assembly instance → exit 3.**
- **Two or more candidates for the same instance → exit 3**, printing the candidate list and
  **naming the option that disambiguates**. Exit 3's meaning widens from "assembly missing" to
  "assembly discovery failed". **Guessing was rejected**: newest-mtime silently picks a stale
  Release build over a fresh Debug one about as often as not, converting a loud stop into
  under-selection with no notice.
- Two things are **not** missing assemblies and must not trip the error: a project outside the
  analysis scope, and a project that produces no output assembly (generator and analyser
  projects, and `ReferenceOutputAssembly=false` references) — both excluded from the expected
  set by ticket 04.

**`TargetPlatformAttribute` is read for a reason beyond completeness**: its presence and value
separate a platform-suffixed instance from a plain one, so `net10.0` and `net10.0-windows`
resolve as two distinct assembly instances and the ambiguity error does **not** misfire. What it
cannot do is recover the *declared* moniker string — it always carries a version, so
`net10.0-windows` reads back as `Windows7.0`, and a project declaring `net10.0-windows7.0`
produces byte-identical attributes. Only the command line needs that string; ticket 13 owns the
consequence.

**Layout options are narrowing hints, not inputs.** `-c|--configuration`, `-o|--output` and
`--artifacts-path` are **filters applied to scan results**, all optional, all spelled exactly as
`dotnet build` spells them. Their only job is to break the ambiguity above, which is why none is
required for the common case. `--artifacts-path` cascades to `--no-build`.

**Stale output**, each case for its own reason: output for a project no longer in the solution
matches no expected instance and is ignored; output for an abandoned target framework likewise;
a stale assembly whose own source changed is caught by ticket 08 (exit 5); a stale assembly whose
own source did not change but whose dependencies did is a **register entry**, `--no-build` only,
direction under-selection, detectable only by comparing dependency timestamps, which M1 does not
do.

## Acceptance criteria

- Build failure exits 2 with no report section claiming a selection.
- `--no-build` never invokes the build adapter, asserted through the fake.
- Discovery resolves the same assembly instance set across **four relocations of the same built
  output**: default `bin/<config>/<tfm>/`, flat, artifacts-style, and a single-targeted project
  with no TFM segment. Relocating already-built output is the cheap way to test this — no
  project per layout.
- A multi-targeted project resolves to several instances, distinguished by
  `TargetFrameworkAttribute`.
- `net10.0` and `net10.0-windows` resolve as two instances, not one ambiguity error.
- Debug and Release both present → exit 3, message lists both candidates and names `-c`.
- A missing expected assembly → exit 3.
- A generator project and a `ReferenceOutputAssembly=false` project produce no missing-assembly
  error.
- A third-party assembly sitting in the output with a first-party-looking name is classified
  non-first-party because its symbols point outside the working tree — asserted, since a PDB
  beside it proves nothing.

## Out of scope

The checksum comparison itself — ticket 08, which consumes this ticket's document enumeration.

## Comments

**Implemented** in `Reach.Core/Assemblies` (`AssemblyFacts`, `PdbDocuments`/`SourceDocument`,
`TargetFrameworkMoniker`, `AssemblyScanner`, `LayoutHints`, `AssemblyDiscovery`) and
`Reach.Core/Build/BuildAdapter.cs`.

**One rule the ticket does not have, and without it the ambiguity error fires on every run:
candidates are grouped by module version id.** A project reference copies its output into every
consuming project's bin directory, so `Core.dll` is genuinely present two or three times in any
ordinary solution. Two files carrying one MVID are one build of one assembly; two MVIDs are two
builds, and *that* is the case discovery refuses to choose between. Debug-versus-Release still
exits 3, and the copy under the project's own directory is the one reported — deterministically,
because the report has to be byte-identical between runs. Both halves have a named test.

**`TargetFrameworkMoniker` is a third form.** Neither spelling can be derived from the other —
the project file says `net10.0-windows`, the assembly stamps `.NETCoreApp,Version=v10.0` plus
`Windows7.0` — so both are parsed into identifier, version and platform-without-version, and
compared there. The declared platform version is deliberately dropped, because
`net10.0-windows` and `net10.0-windows7.0` stamp byte-identical attributes and comparing it
would make one of them never match.

**`Compiled`** (`tests/Reach.Tests/Fixtures/Compiled.cs`) is the fixture that makes §16.2's
"anything testable in memory is tested in memory" real: a source string becomes a real assembly
with a real portable PDB, with the target-framework and target-platform attributes stamped and
the document paths chosen by the test. That is what lets the four-relocation assertion be four
`WriteTo` calls rather than four projects, and what lets a "third-party assembly with a
first-party-looking name" be one line. Tickets 08 onwards should build on it.

One thing it needs and the compiler insists on: `SourceText.From(source, Encoding.UTF8)`.
Without an explicit encoding Roslyn refuses to emit debug information at all (`CS8055`),
because it cannot compute a document checksum. UTF-8 is what the real compiler reads a `.cs`
file as, so the checksums match a file on disk — which ticket 08 depends on.

**Layout hints filter, they never compute.** `-c` matches a whole path segment
case-insensitively on every platform, because artifacts output lower-cases the configuration
name and the default layout does not. `-o` and `--artifacts-path` are containment tests. The
ambiguity message names whichever option has not been tried yet.

**An assembly with no readable symbols is not first-party**, and therefore reads as missing —
exit 3 — rather than as analysed. That is the loud direction, and it is the same code path that
classifies a genuine third-party assembly whose PDB happens to sit beside it.

Not implemented here, as the ticket says: the checksum comparison (ticket 08) consumes
`ScannedAssembly.Documents`, which already carries each document's algorithm and digest.
