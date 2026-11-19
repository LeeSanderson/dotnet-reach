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

- [ ] An assembly compiled with `/_/` mapped to a subdirectory of the repository fails the run with that message (in-memory compilation with a path map)
- [ ] A correct `/_/` build, and a build where only generated documents are missing, are unaffected
- [ ] ADR-0018 and `docs/limitations.md` are updated
- [ ] Full suite, `-warnaserror` build and format clean; a CI-condition run on Windows and in the Linux container

## Comments
