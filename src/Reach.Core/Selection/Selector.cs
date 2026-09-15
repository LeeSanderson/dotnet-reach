using Reach.Assemblies;
using Reach.Changes;
using Reach.Graph;
using Reach.Join;
using Reach.Projects;
using Reach.Reporting;

namespace Reach.Selection;

/// <summary>
/// Walks backwards from the changed set to the tests it can reach, and records why each one was
/// selected and how much to trust it.
/// </summary>
internal static class Selector
{
    internal static SelectionResult Select(
        CallGraph graph,
        IReadOnlyList<GraphAssembly> assemblies,
        IReadOnlyList<AssemblyInstance> instances,
        AnalysisScope scope,
        ChangedSet changed,
        IReadOnlyList<JoinResult> joined,
        bool includePaths)
    {
        var notices = new List<Notice>();
        var roots = new RootSets(assemblies);
        var changes = roots.ChangesFrom(changed, joined, scope);

        // Every test in every in-scope test project, not only the selected ones: the totals,
        // the rendered-match computation and the over-selection measurement all read the list.
        var projects = Enumerate(assemblies, instances, scope, notices);

        var allTests = projects
            .SelectMany(project => project.Tests)
            .ToDictionary(test => test.Id);

        var reachedBy = new Dictionary<MethodId, List<int>>();
        var classOf = new Dictionary<MethodId, PathClass>();
        var pathsOf = includePaths ? new Dictionary<MethodId, List<IReadOnlyList<MethodId>>>() : null;

        foreach (var change in changes)
        {
            if (change.Roots.Count == 0)
            {
                continue;
            }

            var ownRoots = change.Roots.ToHashSet();
            var reached = ReverseWalk.From(graph, change.Roots);

            foreach (var (method, pathClass) in reached)
            {
                // A test that is itself a root of this change is selected by
                // own-source-changed, not by having walked anywhere.
                if (!allTests.ContainsKey(method) || ownRoots.Contains(method))
                {
                    continue;
                }

                Bucket(reachedBy, method).Add(change.Entry.Index);
                change.Entry.TestsReached++;

                classOf[method] = classOf.TryGetValue(method, out var existing)
                    ? (PathClass)Math.Min((int)existing, (int)pathClass)
                    : pathClass;
            }

            if (pathsOf is not null)
            {
                RecordPaths(graph, change, allTests, reached, pathsOf);
            }
        }

        var ownChanged = OwnDeclarationChanged(joined, MemberChange.Modified, MemberChange.Added);
        var added = OwnDeclarationChanged(joined, MemberChange.Added);

        return new SelectionResult(
            [
                .. projects.Select(project => Decide(project, reachedBy, classOf, ownChanged, added, pathsOf))
            ],
            [.. changes.Select(change => change.Entry)],
            notices);
    }

    private static ProjectSelection Decide(
        EnumeratedProject project,
        Dictionary<MethodId, List<int>> reachedBy,
        Dictionary<MethodId, PathClass> classOf,
        HashSet<MethodId> ownChanged,
        HashSet<MethodId> added,
        Dictionary<MethodId, List<IReadOnlyList<MethodId>>>? pathsOf)
    {
        if (project.Framework is null)
        {
            // Whole-project selection: the standard response to missing information. The total
            // is unknown, never zero.
            return new ProjectSelection(
                project.Project,
                project.TargetFramework,
                SelectionMode.RunAll,
                [],
                null,
                null,
                project.Tests);
        }

        var selected = new List<SelectedTest>();

        foreach (var test in project.Tests)
        {
            var rules = new List<SelectionRule>();
            var changes = reachedBy.GetValueOrDefault(test.Id) ?? [];

            if (changes.Count > 0)
            {
                rules.Add(SelectionRule.ReverseReachable);
            }

            if (ownChanged.Contains(test.Id))
            {
                rules.Add(SelectionRule.OwnSourceChanged);
            }

            if (added.Contains(test.Id))
            {
                rules.Add(SelectionRule.NewSinceBaseline);
            }

            if (rules.Count == 0)
            {
                continue;
            }

            selected.Add(new SelectedTest(
                test,
                rules,
                [.. changes.Distinct().Order()],
                changes.Count > 0 ? classOf.GetValueOrDefault(test.Id) : null,
                pathsOf?.GetValueOrDefault(test.Id)));
        }

        return new ProjectSelection(
            project.Project,
            project.TargetFramework,
            // An empty selection emits nothing at all, because an empty filter runs everything.
            selected.Count == 0 ? SelectionMode.Skip : SelectionMode.Filtered,
            [.. selected.OrderBy(test => test.Test.FullyQualifiedName, StringComparer.Ordinal)],
            project.Tests.Count,
            project.Framework.Dialect,
            project.Tests);
    }

    private static IReadOnlyList<EnumeratedProject> Enumerate(
        IReadOnlyList<GraphAssembly> assemblies,
        IReadOnlyList<AssemblyInstance> instances,
        AnalysisScope scope,
        List<Notice> notices)
    {
        var projects = new List<EnumeratedProject>();
        var unrecognised = new List<string>();

        foreach (var instance in instances)
        {
            if (!scope.TestProjects.Contains(instance.Expected.Project))
            {
                continue;
            }

            var assembly = assemblies.FirstOrDefault(candidate =>
                candidate.Name == instance.Assembly.SimpleName
                && candidate.Framework is not null
                && instance.Assembly.Framework is not null
                && candidate.Framework.Matches(instance.Assembly.Framework));

            if (assembly is null)
            {
                continue;
            }

            var framework = TestFrameworks.DetectedIn(assembly.Reader);

            if (framework is null)
            {
                unrecognised.Add(instance.Expected.Project.Name);
            }

            projects.Add(new EnumeratedProject(
                instance.Expected.Project,
                instance.Expected.TargetFramework,
                framework,
                framework is null ? [] : TestMethods.In(assembly, framework)));
        }

        if (unrecognised.Count > 0)
        {
            notices.Add(new Notice(
                NoticeCodes.WholeProjectFallback,
                NoticeKind.Widening,
                $"{unrecognised.Count} test project(s) use a test framework Reach does not "
                + "recognise, so every test in them runs and their totals are unknown: "
                + string.Join(", ", unrecognised.Order(StringComparer.Ordinal)),
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["projects"] = unrecognised.Order(StringComparer.Ordinal).ToArray(),
                }));
        }

        return projects;
    }

    /// <summary>
    /// Which tests' own declarations are in the changed set. Derived from the changed set, not
    /// by comparing two test lists: Reach reads only the current compiled output and cannot
    /// enumerate the baseline's tests without building it.
    /// </summary>
    private static HashSet<MethodId> OwnDeclarationChanged(
        IReadOnlyList<JoinResult> joined,
        params MemberChange[] kinds) =>
    [
        .. joined
            .Where(result => kinds.Contains(result.Member.Change))
            .SelectMany(result => result.Methods)
    ];

    private static void RecordPaths(
        CallGraph graph,
        Change change,
        IReadOnlyDictionary<MethodId, TestMethod> tests,
        IReadOnlyDictionary<MethodId, PathClass> reached,
        Dictionary<MethodId, List<IReadOnlyList<MethodId>>> into)
    {
        var predecessors = ReverseWalk.Predecessors(graph, change.Roots);

        foreach (var method in reached.Keys)
        {
            if (tests.ContainsKey(method))
            {
                Bucket(into, method).Add(ReverseWalk.PathFrom(predecessors, method));
            }
        }
    }

    private static List<T> Bucket<T>(Dictionary<MethodId, List<T>> index, MethodId key)
    {
        if (!index.TryGetValue(key, out var bucket))
        {
            index[key] = bucket = [];
        }

        return bucket;
    }

    private sealed record EnumeratedProject(
        ProjectFile Project,
        string TargetFramework,
        TestFramework? Framework,
        IReadOnlyList<TestMethod> Tests);
}
