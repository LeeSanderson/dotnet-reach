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

    internal bool HasChecksum => Hash.Length > 0;
}

/// <summary>
/// Reads the document table out of an already-opened portable PDB.
/// </summary>
internal static class PdbDocuments
{
    /// <param name="pdbReader">The opened symbols.</param>
    /// <param name="repositoryRoot">
    /// The git top-level of the working tree, which a deterministic build records as
    /// <c>/_/</c>. Empty when git could not report it, and then nothing is mapped.
    /// </param>
    internal static IReadOnlyList<SourceDocument> Of(MetadataReader pdbReader, string repositoryRoot)
    {
        var documents = new List<SourceDocument>(pdbReader.Documents.Count);

        foreach (var handle in pdbReader.Documents)
        {
            var document = pdbReader.GetDocument(handle);

            if (document.Name.IsNil)
            {
                continue;
            }

            var path = PathOf(pdbReader, handle, repositoryRoot);

            documents.Add(new SourceDocument(
                path.Path,
                document.HashAlgorithm.IsNil ? Guid.Empty : pdbReader.GetGuid(document.HashAlgorithm),
                document.Hash.IsNil ? [] : pdbReader.GetBlobBytes(document.Hash))
            {
                UnmappedRoot = path.UnmappedRoot,
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
    /// Deterministic source paths. A build with <c>ContinuousIntegrationBuild</c> records the
    /// git top-level as <c>/_/</c> — the SDK numbers source-controlled roots first, and nests
    /// submodules under it — so <c>/_/</c> maps back to <paramref name="repositoryRoot"/>.
    /// Further roots (<c>/_1/</c>, ...) are ones the build added itself, and nothing in the
    /// symbols says which directory they were, so they are left unmapped rather than guessed.
    /// See ADR-0018.
    /// </item>
    /// </list>
    /// </summary>
    internal static DocumentPath PathOf(MetadataReader pdbReader, DocumentHandle handle, string repositoryRoot) =>
        DeterministicSourcePaths.Map(pdbReader.GetString(pdbReader.GetDocument(handle).Name), repositoryRoot);
}

/// <summary>
/// Where a recorded document sits on this machine.
/// </summary>
/// <param name="Path">Absolute and normalised to this machine's separators.</param>
/// <param name="UnmappedRoot">The deterministic root left unmapped, or null.</param>
internal readonly record struct DocumentPath(string Path, string? UnmappedRoot);

/// <summary>
/// The SDK's deterministic source paths (<c>DeterministicSourcePaths</c>, on by default when
/// <c>ContinuousIntegrationBuild</c> is), read back.
/// </summary>
/// <remarks>
/// <c>MapSourceRoots</c> gives each top-level <c>SourceRoot</c> the prefix
/// <c>"/_" + (index == 0 ? "" : index) + "/"</c>, numbering source-controlled roots first, and
/// gives a nested root (a git submodule) its containing root's prefix plus its relative path.
/// The compiler's <c>-pathmap</c> then rewrites every recorded path. Neither the SourceLink map
/// (prefix to URL) nor the recorded compilation options (which leave <c>pathmap</c> out)
/// record which local directory a prefix stood for.
/// </remarks>
internal static class DeterministicSourcePaths
{
    private const string RepositoryPrefix = "/_/";

    internal static DocumentPath Map(string recorded, string repositoryRoot)
    {
        if (recorded.StartsWith(RepositoryPrefix, StringComparison.Ordinal) && repositoryRoot.Length > 0)
        {
            return new DocumentPath(
                Paths.Normalise(Path.Combine(repositoryRoot, recorded[RepositoryPrefix.Length..])),
                UnmappedRoot: null);
        }

        return new DocumentPath(Paths.Normalise(recorded), UnmappedRootOf(recorded));
    }

    /// <summary>
    /// <c>/_N/</c> for a positive N. <c>/_/</c> itself only when there was no repository root
    /// to map it to.
    /// </summary>
    private static string? UnmappedRootOf(string recorded)
    {
        if (!recorded.StartsWith("/_", StringComparison.Ordinal))
        {
            return null;
        }

        var end = recorded.IndexOf('/', 2);

        if (end < 0)
        {
            return null;
        }

        var number = recorded.AsSpan(2, end - 2);

        foreach (var character in number)
        {
            if (!char.IsAsciiDigit(character))
            {
                return null;
            }
        }

        return recorded[..(end + 1)];
    }
}
