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

- [ ] An assembly compiled with a `/_/` path map is first-party and its documents resolve to files in the working tree (in-memory compilation with a path map)
- [ ] Correspondence verifies such an assembly, and the span join matches its sequence points
- [ ] A document under a further root (`/_1/`, ...) is not guessed at; the run says so in a notice
- [ ] `PortablePdbChecksumTests` no longer maps `/_/` itself
- [ ] Dogfood: this repository built with `GITHUB_ACTIONS=true` and run through `reach select` recognises its first-party assemblies
- [ ] Build and format gates remain clean; all tests pass

## Comments
