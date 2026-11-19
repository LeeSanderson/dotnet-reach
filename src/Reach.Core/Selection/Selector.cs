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
        CallGraphResult graph,
        IReadOnlyList<AssemblyInstance> instances,
        AnalysisScope scope,
        ChangedSet changed,
        IReadOnlyList<JoinResult> joined,
        bool includePaths,
        TierLadder ladder)
    {
        var notices = new List<Notice>();
        var roots = new RootSets(graph.Assemblies);
        var changes = roots.ChangesFrom(changed, joined, scope, ladder);

        if (TierLadder.UnattributedNotice(ladder.RouteAll(changed.UnmappablePaths)) is { } unattributed)
        {
            notices.Add(unattributed);
        }

        var projects = TestEnumeration.Enumerate(graph.Assemblies, instances, scope, notices);

        var allTests = projects
            .SelectMany(project => project.Tests)
            .ToDictionary(test => test.Id);

        var reached = new ReachedTests(graph.Graph, allTests, includePaths);

        foreach (var change in changes)
        {
            reached.Record(change);
        }

        var ownChanged = OwnDeclarationChanged(joined, MemberChange.Modified, MemberChange.Added);
        var added = OwnDeclarationChanged(joined, MemberChange.Added);

        return new SelectionResult(
            [
                .. projects.Select(project => Decide(project, reached, ownChanged, added))
            ],
            [.. changes.Select(change => change.Entry)],
            notices);
    }

    private static ProjectSelection Decide(
        EnumeratedProject project,
        ReachedTests reached,
        HashSet<MethodId> ownChanged,
        HashSet<MethodId> added)
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
            var changes = reached.ChangesReaching(test.Id);
            var rules = RulesFor(test.Id, changes, ownChanged, added);

            if (rules.Count == 0)
            {
                continue;
            }

            selected.Add(new SelectedTest(
                test,
                rules,
                [.. changes.Distinct().Order()],
                changes.Count > 0 ? reached.PathClassOf(test.Id) : null,
                reached.PathsTo(test.Id)));
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

    /// <summary>
    /// Every rule that selects <paramref name="test"/>. None means it is not selected.
    /// </summary>
    private static List<SelectionRule> RulesFor(
        MethodId test,
        List<int> changes,
        HashSet<MethodId> ownChanged,
        HashSet<MethodId> added)
    {
        var rules = new List<SelectionRule>();

        if (changes.Count > 0)
        {
            rules.Add(SelectionRule.ReverseReachable);
        }

        if (ownChanged.Contains(test))
        {
            rules.Add(SelectionRule.OwnSourceChanged);
        }

        if (added.Contains(test))
        {
            rules.Add(SelectionRule.NewSinceBaseline);
        }

        return rules;
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
}
