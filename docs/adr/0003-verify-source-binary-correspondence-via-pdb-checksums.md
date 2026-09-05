# Verify source-binary correspondence via PDB source checksums

Reach checks that the assemblies it reads were compiled from the source it diffed, by
comparing the current source files against the per-document checksums recorded in the
portable debug symbols. This runs in both build modes, not only under `--no-build`.

PRD §5 concluded that freshness cannot be established, because it considered only file
timestamps — which git does not preserve, so a checkout onto a warm agent can leave source
older than the binaries beside it. Source checksums are not a timestamp heuristic; they
are the compiler's own record of the bytes it compiled. **This supersedes §5's position
that freshness checking is not Reach's job**, and turns a prominently documented hazard
into an enforced precondition.

## Consequences

Debug symbols must be present in the build output, regardless of which mechanism the join
seam ends up using. This costs nothing: `DebugType` defaults to `portable` in Release as
well as Debug, so no one is forced into a Debug build. Note that `$(DebugSymbols)` is a
false signal — it evaluates `false` in Release while a PDB is still produced — so the
property to read is `DebugType`.

Symbols beside an assembly prove nothing about its provenance: `.pdb` is an
`AllowedReferenceRelatedFileExtension`, so dependencies' symbols are copied into the
consuming project's output too. First-party classification must therefore resolve the
document paths recorded inside the symbols, never check whether a file exists beside the
assembly.

Source-generated documents have no file on disk to hash and need an explicit skip rule. Reading binaries from a different commit is caught rather than
silently under-selected, which is what PRD §8's invariant requires.

The claim that portable PDBs record per-document source checksums is load-bearing and has
not been verified against real build output. Verifying it is an acceptance criterion of
the implementation ticket, before the design is relied on.
