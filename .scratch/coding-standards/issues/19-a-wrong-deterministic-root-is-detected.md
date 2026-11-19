# 19: A deterministic root that is not the repository is detected

**What to build:** Ticket 14 reads a recorded `/_/` as the git top-level. That is wrong in one configuration: a build with source-control queries off (`EnableSourceControlManagerQueries=false`) and a hand-written `SourceRoot` that is not the repository root. `/_/` then stood for another directory. Where the mapped paths name no file, the documents read as generated, nothing is verified, and joins miss, all silently. Reach detects this and stops with a message that names the likely cause and the workaround. See ticket 14 and ADR-0018.

Maintainer decisions (ticket 14's open questions):
- Mapping further roots through the NuGet global packages folder: "Approve".
- The undetectable gap: "Want detection".

- Detect an assembly whose `/_/`-mapped documents do not resolve to files in the working tree, at the cheapest honest seam.
- Say what it means: the build's `SourceRoot` / `PathMap` gave `/_/` to a directory other than the git top-level. Name the workaround, `-p:DeterministicSourcePaths=false`.
- Amend ADR-0018 and narrow or remove the register entry.

**Blocked by:** None

**Status:** ready-for-agent

- [x] An assembly compiled with `/_/` mapped to a subdirectory of the repository fails the run with that message (in-memory compilation with a path map)
- [x] A correct `/_/` build, and a build where only generated documents are missing, are unaffected
- [x] ADR-0018 and `docs/limitations.md` are updated
- [x] Full suite, `-warnaserror` build and format clean; a CI-condition run on Windows and in the Linux container

## Comments

**Design.** Detection is in correspondence, which already checks every document's existence, so it needs no new disk access. It runs per assembly, because `SourceRoot` is per project. The rule: if an assembly has documents recorded under `/_/` and read as the repository root, and none of them exists, the run fails (exit 5). The message names the assembly, the likely cause (a hand-written `SourceRoot` or `PathMap` with source-control queries off) and `-p:DeterministicSourcePaths=false`.

The rule is "none", not "most". A correct mapping always finds the hand-written files the build just compiled, and only a source generator's output is legitimately missing. A threshold would be a guess about how much generated code a project has. A wrong root whose mapped paths name files with other content was already caught as a checksum mismatch. What remains undetected is a wrong root where some mapped paths name files with identical content; the register entry is narrowed to that. `SourceDocument.ReadAsRepositoryRoot` carries the marker from `DeterministicSourcePaths.Map`.

**Tests** (in-memory compilation with a path map):
- `A_deterministic_root_that_was_not_the_repository_fails_naming_the_cause` maps `/_/` to `src/`. It was red first: `Skipped`, silently.
- `A_deterministic_build_with_generated_documents_missing_still_verifies`.

**Checks.**
- Windows: 565/565, `-warnaserror` and format clean.
- `GITHUB_ACTIONS=true CI=true` Release build: 565/565 with `--no-build`. The dogfood `select -c Release --no-build --base HEAD~3` exits 0, so a correct CI build does not trip the check.
- Linux container (`mcr.microsoft.com/dotnet/sdk:10.0`, fresh clone, CI env): `-warnaserror` build clean, format exit 0, 565/565.
