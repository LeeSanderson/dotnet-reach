using System.Reflection.Metadata;

namespace Reach.Assemblies;

/// <summary>
/// One source file an assembly's debug symbols point at, with the checksum the compiler
/// recorded for it.
/// </summary>
/// <param name="Path">Absolute and normalised to this machine's separators.</param>
/// <param name="HashAlgorithm">
/// The algorithm GUID — SHA-256 for every modern build, SHA-1 for older ones. Carried rather
/// than assumed, because comparing a file against the wrong algorithm's digest fails in the
/// direction that stops a run.
/// </param>
/// <param name="Hash">The recorded digest; empty when the symbols carry none.</param>
public sealed record SourceDocument(string Path, Guid HashAlgorithm, byte[] Hash)
{
    /// <summary>
    /// The deterministic source root (<c>/_1/</c>, <c>/_2/</c>, ...) the document was recorded
    /// under when Reach could not map it to a directory; null for every other document. Its
    /// <see cref="Path"/> then names no file in the working tree.
    /// </summary>
    public string? UnmappedRoot { get; init; }

    /// <summary>
    /// Recorded under a deterministic build's <c>/_/</c> and read as the repository root. A
    /// convention, not a recorded fact, so correspondence checks it held (ADR-0018).
    /// </summary>
    public bool ReadAsRepositoryRoot { get; init; }

    internal bool HasChecksum => Hash.Length > 0;
}

/// <summary>
/// Reads the document table out of an already-opened portable PDB.
/// </summary>
internal static class PdbDocuments
{
    /// <param name="pdbReader">The opened symbols.</param>
    /// <param name="roots">Where a deterministic build's prefixes can point on this machine.</param>
    internal static IReadOnlyList<SourceDocument> Of(MetadataReader pdbReader, SourceRoots roots)
    {
        var documents = new List<SourceDocument>(pdbReader.Documents.Count);

        foreach (var handle in pdbReader.Documents)
        {
            var document = pdbReader.GetDocument(handle);

            if (document.Name.IsNil)
            {
                continue;
            }

            var path = PathOf(pdbReader, handle, roots);

            documents.Add(new SourceDocument(
                path.Path,
                document.HashAlgorithm.IsNil ? Guid.Empty : pdbReader.GetGuid(document.HashAlgorithm),
                document.Hash.IsNil ? [] : pdbReader.GetBlobBytes(document.Hash))
            {
                UnmappedRoot = path.UnmappedRoot,
                ReadAsRepositoryRoot = path.ReadAsRepositoryRoot,
            });
        }

        return documents;
    }

    /// <summary>
    /// The one place a raw PDB document name becomes a path, so no consumer sees the build
    /// machine's spelling of it. Two things differ from the file on this machine:
    /// <list type="bullet">
    /// <item>Separators. A Windows build read on Linux records <c>src\deep/Widget.cs</c>.</item>
    /// <item>
    /// Deterministic source paths. A build with <c>ContinuousIntegrationBuild</c> records
    /// <c>/_/</c> for the git top-level and <c>/_1/</c>, ... for further roots, such as the
    /// NuGet package folders. See <see cref="DeterministicSourcePaths"/> and ADR-0018.
    /// </item>
    /// </list>
    /// </summary>
    internal static DocumentPath PathOf(MetadataReader pdbReader, DocumentHandle handle, SourceRoots roots) =>
        DeterministicSourcePaths.Map(pdbReader.GetString(pdbReader.GetDocument(handle).Name), roots);
}
