using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Reach.Assemblies;

/// <summary>One candidate file, opened and read.</summary>
/// <param name="IsFirstParty">
/// True when the debug symbols point at source inside the working tree. Symbols sitting
/// <em>beside</em> an assembly prove nothing — <c>.pdb</c> is an
/// <c>AllowedReferenceRelatedFileExtension</c>, so dependencies' symbols are copied into
/// consuming projects' output — so this resolves the document paths recorded <em>inside</em>
/// the symbols and never checks whether a file exists beside the assembly.
/// </param>
internal sealed record ScannedAssembly(
    string Path,
    string SimpleName,
    Guid ModuleVersionId,
    TargetFrameworkMoniker? Framework,
    bool IsFirstParty,
    IReadOnlyList<SourceDocument> Documents);

/// <summary>
/// Finds assemblies on disk by looking, never by predicting.
/// </summary>
/// <remarks>
/// <para>
/// Layout is undecidable from disk. The same <c>OutputPath</c> produces
/// <c>custom/out/net10.0/</c> when set in the project file and <c>custom/out/</c> flat when
/// forwarded as an MSBuild global property — which is what <c>dotnet build -o</c> does, because
/// global properties cannot be reassigned during evaluation — and <strong>nothing on disk
/// records which happened</strong>. Artifacts output is a third layout, in which a
/// single-targeted project gets no target-framework segment at all.
/// </para>
/// <para>
/// So every ambiguous case falls out rather than needing a rule, because no path is ever
/// computed.
/// </para>
/// </remarks>
internal static class AssemblyScanner
{
    /// <summary>
    /// Never searched. <c>obj/</c> holds reference assemblies and intermediate copies that
    /// would double every candidate; the rest are not build output at all.
    /// </summary>
    private static readonly string[] SkippedDirectories =
        ["obj", ".git", "node_modules", ".vs", "packages"];

    /// <summary>
    /// Enumerates, prunes by file name, then opens only the survivors. The prune is the cheap
    /// filter and it runs before anything is opened, which is what keeps a scan over a large
    /// repository affordable.
    /// </summary>
    /// <param name="root">The target's directory tree — the solution's, or the project's.</param>
    /// <param name="expectedNames">Assembly simple names worth opening a file for.</param>
    /// <param name="sourceRoot">The working tree. What "first-party" is measured against.</param>
    internal static IReadOnlyList<ScannedAssembly> Scan(
        string root,
        IReadOnlySet<string> expectedNames,
        string sourceRoot)
    {
        var scanned = new List<ScannedAssembly>();

        foreach (var file in Candidates(root))
        {
            if (!expectedNames.Contains(Path.GetFileNameWithoutExtension(file)))
            {
                continue;
            }

            var assembly = Read(file, sourceRoot);

            if (assembly is not null)
            {
                scanned.Add(assembly);
            }
        }

        return scanned;
    }

    internal static ScannedAssembly? Read(string file, string sourceRoot)
    {
        try
        {
            using var stream = File.OpenRead(file);
            using var peReader = new PEReader(stream);

            if (!peReader.HasMetadata)
            {
                return null;
            }

            var reader = peReader.GetMetadataReader();

            if (!reader.IsAssembly)
            {
                return null;
            }

            var documents = DocumentsOf(peReader, file);

            return new ScannedAssembly(
                Paths.Normalise(file),
                AssemblyFacts.SimpleName(reader),
                AssemblyFacts.ModuleVersionId(reader),
                TargetFrameworkMoniker.FromAttributes(
                    AssemblyFacts.TargetFramework(reader),
                    AssemblyFacts.TargetPlatform(reader)),
                documents.Any(document => Paths.IsUnder(document.Path, sourceRoot)),
                documents);
        }
        catch (BadImageFormatException)
        {
            // A native dll sitting in the output. Not an assembly, not an error.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Handles both a separate <c>.pdb</c> and an embedded one.</summary>
    private static IReadOnlyList<SourceDocument> DocumentsOf(PEReader peReader, string file)
    {
        try
        {
            if (!peReader.TryOpenAssociatedPortablePdb(
                    Path.GetFullPath(file),
                    path => File.Exists(path) ? File.OpenRead(path) : null,
                    out var provider,
                    out _)
                || provider is null)
            {
                return [];
            }

            using (provider)
            {
                return PdbDocuments.Of(provider.GetMetadataReader());
            }
        }
        catch (BadImageFormatException)
        {
            // A Windows PDB, or a corrupt one. No documents means not first-party, which is
            // the loud direction: the assembly then reads as missing rather than as analysed.
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static IEnumerable<string> Candidates(string root)
    {
        var pending = new Stack<string>([root]);

        while (pending.TryPop(out var directory))
        {
            string[] files;
            string[] subdirectories;

            try
            {
                files = Directory.GetFiles(directory, "*.dll");
                subdirectories = Directory.GetDirectories(directory);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var subdirectory in subdirectories)
            {
                if (!SkippedDirectories.Contains(Path.GetFileName(subdirectory), StringComparer.OrdinalIgnoreCase))
                {
                    pending.Push(subdirectory);
                }
            }
        }
    }
}
