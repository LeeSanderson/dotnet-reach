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
    private readonly List<PEReader> readers = [];

    private OpenAssemblies(IReadOnlyList<GraphAssembly> assemblies) => Assemblies = assemblies;

    internal IReadOnlyList<GraphAssembly> Assemblies { get; private set; }

    internal static OpenAssemblies Open(IEnumerable<AssemblyInstance> instances)
    {
        var open = new OpenAssemblies([]);
        var assemblies = new List<GraphAssembly>();

        foreach (var instance in CallGraphBuilder.InOrdinalOrder(instances))
        {
            PEReader reader;

            try
            {
                // The whole image, not just the metadata: method bodies live in the IL
                // sections, and reading those lazily would keep a file handle open on every
                // assembly in the solution for the length of the run.
                reader = new PEReader(
                    File.OpenRead(instance.Assembly.Path),
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

            open.readers.Add(reader);

            assemblies.Add(new GraphAssembly(
                assemblies.Count,
                instance.Assembly.SimpleName,
                instance.Assembly.Framework,
                reader.GetMetadataReader(),
                reader));
        }

        open.Assemblies = assemblies;

        return open;
    }

    public void Dispose()
    {
        foreach (var reader in readers)
        {
            reader.Dispose();
        }
    }
}
