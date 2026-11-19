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
    internal bool HasChecksum => Hash.Length > 0;
}

/// <summary>
/// Reads the document table out of an already-opened portable PDB.
/// </summary>
internal static class PdbDocuments
{
    internal static IReadOnlyList<SourceDocument> Of(MetadataReader pdbReader)
    {
        var documents = new List<SourceDocument>(pdbReader.Documents.Count);

        foreach (var handle in pdbReader.Documents)
        {
            var document = pdbReader.GetDocument(handle);

            if (document.Name.IsNil)
            {
                continue;
            }

            documents.Add(new SourceDocument(
                PathOf(pdbReader, handle),
                document.HashAlgorithm.IsNil ? Guid.Empty : pdbReader.GetGuid(document.HashAlgorithm),
                document.Hash.IsNil ? [] : pdbReader.GetBlobBytes(document.Hash)));
        }

        return documents;
    }

    /// <summary>
    /// The one place a raw PDB document name becomes a path, so no consumer sees the build
    /// machine's separators: a Windows build read on Linux records <c>src\deep/Widget.cs</c>.
    /// </summary>
    internal static string PathOf(MetadataReader pdbReader, DocumentHandle handle) =>
        Paths.Normalise(pdbReader.GetString(pdbReader.GetDocument(handle).Name));
}
