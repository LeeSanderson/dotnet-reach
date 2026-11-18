# 11: PDB document paths are normalised where they are read

**What to build:** On Linux, a PDB that records a document path with Windows or mixed separators (`src\deep/Widget.cs`) still has its documents found, so source-binary correspondence verifies them instead of silently becoming *Skipped*. No consumer of PDB document paths ever sees a raw path. See [the spec](../spec.md) and ADR-0003.

- Add a single helper on the source-document module that turns a raw PDB document name into a normalised path, given the metadata reader and the document handle.
- The source-document list uses it, so every source document's path is normalised when it is read. Correspondence (its de-duplication key, existence check, visibility check and hashing) then works with no changes of its own.
- The span join's sequence-point reading uses the same helper in place of its inline normalisation.
- Remove the tier ladder's now-redundant normalisation of a source document's path. Its normalisation of the working-tree side stays.

**Blocked by:** 09 (integrate and verify the coding standard)

**Status:** ready-for-agent

- [ ] The correspondence test for "a document recorded with mixed separators still finds its file" passes on Linux, unchanged
- [ ] A new span-join test over a PDB whose document is recorded with mixed separators passes on both OSes. Build it with the in-memory compilation helper, as the correspondence tests do; if the helper can't produce that shape, record why under `## Comments` and skip this criterion
- [ ] Exactly one place converts a raw PDB document name to a path
- [ ] All existing correspondence, tier-ladder and span-join tests pass on both OSes
- [ ] Build and format gates remain clean
