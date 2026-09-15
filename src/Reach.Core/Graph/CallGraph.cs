namespace Reach.Graph;

/// <summary>One call edge. No string-typed field, by design and by test.</summary>
internal readonly record struct Edge(MethodId From, MethodId To, EdgeProvenance Provenance);

/// <summary>The callers of one node, with the provenance of each edge.</summary>
internal readonly ref struct IncomingEdges(ReadOnlySpan<MethodId> sources, ReadOnlySpan<EdgeProvenance> provenances)
{
    internal ReadOnlySpan<MethodId> Sources { get; } = sources;

    internal ReadOnlySpan<EdgeProvenance> Provenances { get; } = provenances;

    internal int Count => Sources.Length;
}

/// <summary>
/// A directed graph whose nodes are methods and whose edges mean "this method can invoke that
/// one". Built from compiled assemblies, never from source.
/// </summary>
/// <remarks>
/// <strong>Only the reverse index is built.</strong> Reverse reachability walks backwards; the
/// forward change list carries counts derived from the walk's roots; opt-in hop-by-hop paths
/// reconstruct backwards. A forward index would be a second structure obliged to agree with
/// the first.
/// </remarks>
internal sealed class CallGraph
{
    private readonly Dictionary<MethodId, int> indexOf;
    private readonly MethodId[] nodes;

    // Compressed sparse row over the reversed edges: offsets[i]..offsets[i+1] indexes the
    // callers of nodes[i]. Inverted once after the pass rather than maintained during it — the
    // node count is not known until the pass ends.
    private readonly int[] offsets;
    private readonly MethodId[] sources;
    private readonly EdgeProvenance[] provenances;

    private CallGraph(
        Dictionary<MethodId, int> indexOf,
        MethodId[] nodes,
        int[] offsets,
        MethodId[] sources,
        EdgeProvenance[] provenances)
    {
        this.indexOf = indexOf;
        this.nodes = nodes;
        this.offsets = offsets;
        this.sources = sources;
        this.provenances = provenances;
    }

    internal IReadOnlyList<MethodId> Nodes => nodes;

    internal int EdgeCount => sources.Length;

    internal bool Contains(MethodId id) => indexOf.ContainsKey(id);

    /// <summary>Every method that can invoke <paramref name="id"/>. The only direction the graph indexes.</summary>
    internal IncomingEdges CallersOf(MethodId id)
    {
        if (!indexOf.TryGetValue(id, out var index))
        {
            return default;
        }

        var start = offsets[index];
        var length = offsets[index + 1] - start;

        return new IncomingEdges(
            sources.AsSpan(start, length),
            provenances.AsSpan(start, length));
    }

    /// <summary>
    /// Inverts a flat edge list into the reverse index. Every node an edge names is a node,
    /// whether or not it was declared: an external widening anchor has no definition to
    /// enumerate.
    /// </summary>
    internal static CallGraph Build(IEnumerable<MethodId> declared, IReadOnlyList<Edge> edges)
    {
        var indexOf = new Dictionary<MethodId, int>();
        var nodes = new List<MethodId>();

        void Add(MethodId id)
        {
            if (indexOf.TryAdd(id, nodes.Count))
            {
                nodes.Add(id);
            }
        }

        foreach (var id in declared)
        {
            Add(id);
        }

        foreach (var edge in edges)
        {
            Add(edge.From);
            Add(edge.To);
        }

        // Counting sort by target: one pass to count, one to place. Sorting the edge array
        // itself would be the same work plus a comparison per element.
        var offsets = new int[nodes.Count + 1];

        foreach (var edge in edges)
        {
            offsets[indexOf[edge.To] + 1]++;
        }

        for (var index = 0; index < nodes.Count; index++)
        {
            offsets[index + 1] += offsets[index];
        }

        var sources = new MethodId[edges.Count];
        var provenances = new EdgeProvenance[edges.Count];
        var cursor = (int[])offsets.Clone();

        foreach (var edge in edges)
        {
            var slot = cursor[indexOf[edge.To]]++;
            sources[slot] = edge.From;
            provenances[slot] = edge.Provenance;
        }

        return new CallGraph(indexOf, [.. nodes], offsets, sources, provenances);
    }
}
