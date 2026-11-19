using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Reach.Assemblies;

namespace Reach.Graph;

/// <summary>
/// Opens the discovered assembly instances for the graph pass and keeps them open for its
/// duration.
/// </summary>
/// <remarks>
/// Ordinals are assigned here, in a deterministic order — assembly simple name, then target
/// framework — so a debugging session is reproducible. No ordinal ever reaches the report, so
/// report determinism does not depend on it.
/// </remarks>
internal sealed class OpenAssemblies : IDisposable
{
    private readonly List<PEReader> _readers = [];
    private readonly List<MetadataReaderProvider> _symbols = [];

    private OpenAssemblies(IReadOnlyList<GraphAssembly> assemblies) => Assemblies = assemblies;

    internal IReadOnlyList<GraphAssembly> Assemblies { get; private set; }

    internal static OpenAssemblies Open(IEnumerable<AssemblyInstance> instances)
    {
        var open = new OpenAssemblies([]);
        var assemblies = new List<GraphAssembly>();

        foreach (var assembly in InOrdinalOrder(instances).Select(instance => instance.Assembly))
        {
            PEReader reader;

            try
            {
                // The whole image, not just the metadata: method bodies live in the IL
                // sections, and reading those lazily would keep a file handle open on every
                // assembly in the solution for the length of the run.
                reader = new PEReader(
                    File.OpenRead(assembly.Path),
                    PEStreamOptions.PrefetchEntireImage);
            }
            catch (IOException)
            {
                continue;
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            open._readers.Add(reader);

            assemblies.Add(new GraphAssembly(
                assemblies.Count,
                assembly.SimpleName,
                assembly.Framework,
                reader.GetMetadataReader(),
                reader,
                open.OpenSymbols(reader, assembly.Path)));
        }

        open.Assemblies = assemblies;

        return open;
    }
    /// <summary>
    /// Assembly simple name, then target framework: the deterministic order ordinals follow.
    /// </summary>
    private static IReadOnlyList<AssemblyInstance> InOrdinalOrder(IEnumerable<AssemblyInstance> instances) =>
    [
        .. instances
            .OrderBy(instance => instance.Assembly.SimpleName, StringComparer.Ordinal)
            .ThenBy(instance => instance.Expected.TargetFramework, StringComparer.Ordinal)
    ];

    /// <summary>
    /// Handles both a separate <c>.pdb</c> and symbols embedded in the image.
    /// </summary>
    private MetadataReader? OpenSymbols(PEReader reader, string path)
    {
        try
        {
            if (!reader.TryOpenAssociatedPortablePdb(
                    Path.GetFullPath(path),
                    candidate => File.Exists(candidate) ? File.OpenRead(candidate) : null,
                    out var provider,
                    out _)
                || provider is null)
            {
                return null;
            }

            _symbols.Add(provider);

            return provider.GetMetadataReader();
        }
        catch (BadImageFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var provider in _symbols)
        {
            provider.Dispose();
        }

        foreach (var reader in _readers)
        {
            reader.Dispose();
        }
    }
}
