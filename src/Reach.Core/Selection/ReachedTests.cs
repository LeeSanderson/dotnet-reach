using Reach.Graph;
using static Reach.Buckets;

namespace Reach.Selection;

/// <summary>
/// The tests each change reaches by reverse reachability, accumulated change by change: which
/// changes reached each test, the strongest path class among them, and, when asked for, the
/// hop-by-hop paths.
/// </summary>
internal sealed class ReachedTests(
    CallGraph graph,
    IReadOnlyDictionary<MethodId, TestMethod> tests,
    bool includePaths)
{
    private readonly ChangesByTest _reachedBy = new();
    private readonly Dictionary<MethodId, PathClass> _classOf = [];
    private readonly PathsByTest? _pathsOf = includePaths ? new PathsByTest() : null;

    /// <summary>
    /// Walks back from one change's roots and records every test it reaches.
    /// </summary>
    internal void Record(Change change)
    {
        if (change.Roots.Count == 0)
        {
            return;
        }

        // A test that is itself a root is excluded only at the member tier, where the roots
        // *are* the changed declaration and own-source-changed picks the test up instead —
        // two attributions for one fact read badly. A widening's roots are an expansion, so
        // nothing else would select the tests among them and excluding them under-selects.
        var ownRoots = change.Entry.Tier == ChangeTier.Member ? change.Roots.ToHashSet() : [];
        var reached = ReverseWalk.From(graph, change.Roots);

        foreach (var (method, pathClass) in reached)
        {
            if (!tests.ContainsKey(method) || ownRoots.Contains(method))
            {
                continue;
            }

            Bucket(_reachedBy, method).Add(change.Entry.Index);
            change.Entry.TestsReached++;

            _classOf[method] = _classOf.TryGetValue(method, out var existing)
                ? Closer(existing, pathClass)
                : pathClass;
        }

        if (_pathsOf is not null)
        {
            RecordPaths(change, reached, ownRoots, _pathsOf);
        }
    }

    /// <summary>
    /// The index of every change that reached <paramref name="test"/>, once per reaching.
    /// </summary>
    internal List<int> ChangesReaching(MethodId test) => _reachedBy.GetValueOrDefault(test) ?? [];

    /// <summary>
    /// The strongest path class by which any change reached <paramref name="test"/>.
    /// </summary>
    internal PathClass PathClassOf(MethodId test) => _classOf.GetValueOrDefault(test);

    /// <summary>
    /// The hop-by-hop paths to <paramref name="test"/>, or null when paths were not asked for or
    /// none reached it.
    /// </summary>
    internal List<IReadOnlyList<MethodId>>? PathsTo(MethodId test) => _pathsOf?.GetValueOrDefault(test);

    /// <summary>
    /// The stronger of two path classes: a compiled path beats a widened one.
    /// </summary>
    private static PathClass Closer(PathClass first, PathClass second) => first < second ? first : second;

    /// <summary>
    /// Records, for every test one change reached, the hop-by-hop path from the test to the change.
    /// </summary>
    /// <param name="change">The change whose roots the paths lead back to.</param>
    /// <param name="reached">Every method the change reached.</param>
    /// <param name="ownRoots">
    /// The same exclusion the counting loop applies, and for the same reason: a test excluded
    /// there carries no change index, so recording a path for it here would put a path in the
    /// report next to an empty <c>changes</c> — a hop-by-hop answer to a question the report
    /// says was never asked.
    /// </param>
    /// <param name="into">Where the paths go, keyed by test.</param>
    private void RecordPaths(
        Change change,
        IReadOnlyDictionary<MethodId, PathClass> reached,
        IReadOnlySet<MethodId> ownRoots,
        PathsByTest into)
    {
        var predecessors = ReverseWalk.Predecessors(graph, change.Roots);

        var reachedTests = reached.Keys
            .Where(method => tests.ContainsKey(method) && !ownRoots.Contains(method));

        foreach (var method in reachedTests)
        {
            Bucket(into, method).Add(ReverseWalk.PathFrom(predecessors, method));
        }
    }

    /// <summary>
    /// Per test, the index of every change that reached it.
    /// </summary>
    private sealed class ChangesByTest : Dictionary<MethodId, List<int>>;

    /// <summary>
    /// Per test, the hop-by-hop path from every change that reached it.
    /// </summary>
    private sealed class PathsByTest : Dictionary<MethodId, List<IReadOnlyList<MethodId>>>;
}
