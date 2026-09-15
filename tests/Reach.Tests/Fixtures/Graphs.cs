using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Reach.Assemblies;
using Reach.Graph;

namespace Reach.Tests.Fixtures;

/// <summary>
/// Builds a call graph from source strings, with nothing on disk.
/// </summary>
/// <remarks>
/// In-memory compilation runs in milliseconds and can produce any IL shape on demand, and the
/// graph pass takes already-opened readers — so this is the whole fixture: a source string in,
/// a graph out.
/// </remarks>
internal sealed class Graphs : IDisposable
{
    private readonly List<PEReader> readers = [];

    internal CallGraphResult Result { get; private set; } = null!;

    internal CallGraph Graph => Result.Graph;

    /// <summary>Compiles each named source into its own assembly instance and builds the graph.</summary>
    internal static Graphs Of(params (string Name, string Source)[] assemblies)
    {
        var graphs = new Graphs();
        var compiled = new List<CompiledAssembly>();
        var built = new List<GraphAssembly>();

        foreach (var (name, source) in assemblies)
        {
            var assembly = Compiled.Assembly(name, source, $"/repo/{name}.cs", references: [.. compiled]);
            compiled.Add(assembly);

            var reader = new PEReader(ImmutableArray.Create(assembly.Image));
            graphs.readers.Add(reader);

            built.Add(new GraphAssembly(
                built.Count,
                name,
                TargetFrameworkMoniker.Parse("net10.0"),
                reader.GetMetadataReader(),
                reader));
        }

        graphs.Result = CallGraphBuilder.Build(built);
        graphs.Assemblies = built;

        return graphs;
    }

    internal static Graphs Of(string source) => Of(("Subject", source));

    /// <summary>
    /// Compiles the consumer against one revision of a library and then puts a <em>different</em>
    /// revision in the graph — which is what a stale assembly on disk looks like from here.
    /// </summary>
    internal static Graphs OfMismatched(string withMember, string withoutMember, string consumer)
    {
        var graphs = new Graphs();

        var compiledAgainst = Compiled.Assembly("Core", withMember, "/repo/Core.cs");
        var onDisk = Compiled.Assembly("Core", withoutMember, "/repo/Core.cs");
        var app = Compiled.Assembly("App", consumer, "/repo/App.cs", references: [compiledAgainst]);

        var built = new List<GraphAssembly>();

        foreach (var (name, assembly) in new[] { ("Core", onDisk), ("App", app) })
        {
            var reader = new PEReader(ImmutableArray.Create(assembly.Image));
            graphs.readers.Add(reader);

            built.Add(new GraphAssembly(
                built.Count,
                name,
                TargetFrameworkMoniker.Parse("net10.0"),
                reader.GetMetadataReader(),
                reader));
        }

        graphs.Result = CallGraphBuilder.Build(built);
        graphs.Assemblies = built;

        return graphs;
    }

    internal IReadOnlyList<GraphAssembly> Assemblies { get; private set; } = [];

    /// <summary>The node for one method, found by its declaring type and name.</summary>
    internal MethodId Method(string declaringType, string name, int assemblyIndex = 0)
    {
        var found = Methods(declaringType, name, assemblyIndex);

        Assert.True(
            found.Count == 1,
            $"Expected exactly one {declaringType}::{name}, found {found.Count}.");

        return found[0];
    }

    internal IReadOnlyList<MethodId> Methods(string declaringType, string name, int assemblyIndex = 0)
    {
        var assembly = Assemblies[assemblyIndex];
        var reader = assembly.Reader;
        var found = new List<MethodId>();

        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);

            if (reader.GetString(method.Name) == name
                && MetadataNames.FullNameOf(reader, method.GetDeclaringType()) == declaringType)
            {
                found.Add(MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(handle)));
            }
        }

        return found;
    }

    /// <summary>Every method name declared by one type, so a test can say what the graph contains.</summary>
    internal IReadOnlyList<string> MethodNamesOf(string declaringType, int assemblyIndex = 0)
    {
        var reader = Assemblies[assemblyIndex].Reader;

        return
        [
            .. reader.MethodDefinitions
                .Select(reader.GetMethodDefinition)
                .Where(method => MetadataNames.FullNameOf(reader, method.GetDeclaringType()) == declaringType)
                .Select(method => reader.GetString(method.Name))
                .Order(StringComparer.Ordinal)
        ];
    }

    /// <summary>The provenances of every edge from <paramref name="from"/> to <paramref name="to"/>.</summary>
    internal IReadOnlyList<EdgeProvenance> EdgesBetween(MethodId from, MethodId to)
    {
        var incoming = Graph.CallersOf(to);
        var found = new List<EdgeProvenance>();

        for (var index = 0; index < incoming.Count; index++)
        {
            if (incoming.Sources[index] == from)
            {
                found.Add(incoming.Provenances[index]);
            }
        }

        return found;
    }

    internal bool HasEdge(MethodId from, MethodId to, EdgeProvenance provenance) =>
        EdgesBetween(from, to).Contains(provenance);

    /// <summary>Every external anchor the pass interned, by its readable name.</summary>
    internal IReadOnlyList<string> ExternalAnchorNames() =>
    [
        .. Graph.Nodes
            .Where(node => node.IsExternal)
            .Select(node => Result.Externals.NameOf(node))
            .OfType<string>()
            .Order(StringComparer.Ordinal)
    ];

    public void Dispose()
    {
        foreach (var reader in readers)
        {
            reader.Dispose();
        }
    }
}
