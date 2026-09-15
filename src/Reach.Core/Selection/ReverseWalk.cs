using Reach.Graph;

namespace Reach.Selection;

/// <summary>
/// How much to trust one test's selection by one change: the weakest edge provenance on the
/// <em>strongest</em> path between them.
/// </summary>
/// <remarks>
/// <strong>This inverts naively taking the worst edge, and that is the point.</strong> A test
/// reachable by any fully compiled path is <see cref="Compiled"/> even if a widened path also
/// exists; a test reachable only through widening is <see cref="Widened"/>, which is where
/// over-selection is plausible and the only class narrowing may ever touch.
/// </remarks>
internal enum PathClass
{
    /// <summary>
    /// Reachable without a widened edge. Synthesised edges count here: containment and type
    /// initialization are invented by Reach, but the control flow they describe is not in doubt.
    /// </summary>
    Compiled,

    /// <summary>Reachable only through a dispatch site whose implementation is not knowable.</summary>
    Widened,
}

/// <summary>
/// Following call edges backwards from a set of roots to everything that could lead to them.
/// The core algorithm.
/// </summary>
internal static class ReverseWalk
{
    /// <summary>
    /// Every method that can reach one of <paramref name="roots"/>, with the strongest path
    /// class each was reached by.
    /// </summary>
    /// <remarks>
    /// Two passes, which is what makes "strongest path" fall out rather than needing a
    /// priority queue: the first follows only non-widened edges, so everything it reaches is
    /// <see cref="PathClass.Compiled"/>; the second follows every edge from what the first
    /// found, so everything newly reached is <see cref="PathClass.Widened"/>.
    /// </remarks>
    internal static Dictionary<MethodId, PathClass> From(CallGraph graph, IReadOnlyCollection<MethodId> roots)
    {
        var reached = new Dictionary<MethodId, PathClass>();

        Spread(graph, roots, reached, PathClass.Compiled, widened: false);
        Spread(graph, [.. reached.Keys], reached, PathClass.Widened, widened: true);

        return reached;
    }

    /// <summary>
    /// The same walk, recording one predecessor per node so a hop-by-hop path can be
    /// reconstructed backwards afterwards. Only <c>--paths</c> asks for this.
    /// </summary>
    internal static Dictionary<MethodId, MethodId> Predecessors(
        CallGraph graph,
        IReadOnlyCollection<MethodId> roots)
    {
        var predecessor = new Dictionary<MethodId, MethodId>();
        var seen = new HashSet<MethodId>(roots);
        var pending = new Queue<MethodId>(roots);

        while (pending.TryDequeue(out var node))
        {
            var incoming = graph.CallersOf(node);

            for (var index = 0; index < incoming.Count; index++)
            {
                var caller = incoming.Sources[index];

                if (seen.Add(caller))
                {
                    predecessor[caller] = node;
                    pending.Enqueue(caller);
                }
            }
        }

        return predecessor;
    }

    /// <summary>
    /// The hops from <paramref name="test"/> down to the root it was reached from, in call
    /// order. Reconstructed backwards over the reverse index, which is the only direction the
    /// graph is indexed in.
    /// </summary>
    internal static IReadOnlyList<MethodId> PathFrom(
        IReadOnlyDictionary<MethodId, MethodId> predecessors,
        MethodId test)
    {
        var hops = new List<MethodId> { test };
        var current = test;

        while (predecessors.TryGetValue(current, out var next))
        {
            hops.Add(next);
            current = next;

            if (hops.Count > 4096)
            {
                // Hop-by-hop paths are the one genuinely unbounded thing in the design, which
                // is why they are opt-in. A cycle would otherwise never terminate.
                break;
            }
        }

        return hops;
    }

    private static void Spread(
        CallGraph graph,
        IReadOnlyCollection<MethodId> from,
        Dictionary<MethodId, PathClass> reached,
        PathClass pathClass,
        bool widened)
    {
        var pending = new Queue<MethodId>(from);

        foreach (var root in from)
        {
            // Already present on the second pass, where every node the first pass reached is a
            // starting point and keeps the stronger class it was given there.
            reached.TryAdd(root, pathClass);
        }

        while (pending.TryDequeue(out var node))
        {
            var incoming = graph.CallersOf(node);

            for (var index = 0; index < incoming.Count; index++)
            {
                if (!widened && incoming.Provenances[index].IsWidened())
                {
                    continue;
                }

                var caller = incoming.Sources[index];

                if (reached.TryAdd(caller, pathClass))
                {
                    pending.Enqueue(caller);
                }
            }
        }
    }
}
