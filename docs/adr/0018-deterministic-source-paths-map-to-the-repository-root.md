# Deterministic source paths map to the repository root

A CI build records source paths with a deterministic prefix instead of the real directory:
`ContinuousIntegrationBuild=true` turns on `DeterministicSourcePaths`, and the SDK passes the
compiler a `-pathmap` built from the `SourceRoot` items. Reach reads a recorded `/_/` as the
git top-level of the working tree, in the one place a PDB document name becomes a path
(`PdbDocuments.PathOf`). Every consumer (first-party classification, correspondence, the tier
ladder, the span join) then sees real paths. Without this, every first-party assembly of a CI
build read as "source outside the working tree" and the run failed. That happens for any
adopter with reproducible builds, which Microsoft recommends for SourceLink.

The mapping is a convention, not something the symbols record. Neither the SourceLink map
(prefix to URL) nor the recorded compilation options (which leave `pathmap` out) say which local
directory a prefix stood for. The convention holds because of how the SDK assigns prefixes
(`MapSourceRoots`): top-level roots are numbered `/_/`, `/_1/`, `/_2/`, ... with source-controlled
roots first, and the git repository is the only source-controlled top-level root. Git submodules
are nested roots, recorded as `/_/<relative path>/`, so they map too.

**Further roots (`/_1/`, ...) are not mapped.** They come only from `SourceRoot` items a build
adds itself, and nothing Reach can read says which directory they were. Searching the working
tree for a directory where the files' checksums agree was considered and rejected: it is a guess
that turns a disclosed gap into a silent one whenever it is wrong. Reach discloses the gap
instead, with the blind-spot notice `unmapped-deterministic-source-root`. An assembly recorded
only under such roots fails discovery, and the message names the cause. Passing
`-p:DeterministicSourcePaths=false` to the build Reach runs was also rejected. It does not help
`--no-build`, where the caller's CI build already happened, and it would change the
adopter's build output.

## Consequences

A `/_/` path cannot be mapped when git cannot report the top-level. Reach then leaves it
unmapped, and the run fails discovery, naming the cause.

The convention can be wrong in one configuration: a build with source-control queries turned
off (`EnableSourceControlManagerQueries=false`) and a hand-written `SourceRoot` that is not the
repository root. `/_/` then stood for a different directory. Where the mapped paths name
existing files with other content, ADR-0003's checksums fail the run. Where they name no file,
the documents read as generated, nothing is verified, and joins miss. That case is registered
in the limitations register rather than detected.
