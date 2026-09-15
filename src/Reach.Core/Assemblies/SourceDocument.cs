using System.Reflection.Metadata;

namespace Reach.Assemblies;

/// <summary>
/// One source file an assembly's debug symbols point at, with the checksum the compiler
/// recorded for it.
/// </summary>
/// <param name="Path">As the symbols spell it: absolute, in the build machine's separators.</param>
/// <param name="HashAlgorithm">
/// The algorithm GUID — SHA-256 for every modern build, SHA-1 for older ones. Carried rather
/// than assumed, because comparing a file against the wrong algorithm's digest fails in the
/// direction that stops a run.
/// </param>
internal sealed record SourceDocument(string Path, Guid HashAlgorithm, byte[] Hash)
{
    internal bool HasChecksum => Hash.Length > 0;
}

/// <summary>Reads the document table out of an already-opened portable PDB.</summary>
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
                pdbReader.GetString(document.Name),
                document.HashAlgorithm.IsNil ? Guid.Empty : pdbReader.GetGuid(document.HashAlgorithm),
                document.Hash.IsNil ? [] : pdbReader.GetBlobBytes(document.Hash)));
        }

        return documents;
    }
}
