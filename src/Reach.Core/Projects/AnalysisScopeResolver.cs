using Reach.Reporting;

namespace Reach.Projects;

/// <summary>
/// Builds the analysis scope from a discovered target.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Changed-side narrowing is forbidden and is the most attractive available mistake.</strong>
/// Nothing here takes the changed set as an input, and nothing should: a caller usually
/// references the interface, not the implementation, so the caller's project is not a
/// dependent of the implementation's project and would be excluded despite being affected.
/// Build scope may be narrowed; changed-side analysis scope may not.
/// </para>
/// </remarks>
internal static class AnalysisScopeResolver
{
    internal static async Task<AnalysisScopeResult> ResolveAsync(
        DiscoveredTarget target,
        CancellationToken cancellationToken = default)
    {
        var notices = new List<Notice>();

        var roots = target.Kind == TargetKind.Solution
            ? await SolutionReader.ReadProjectPathsAsync(target.Path, cancellationToken).ConfigureAwait(false)
            : [target.Path];

        if (target.Kind == TargetKind.Project)
        {
            await NoteIfOutsideEverySolutionAsync(target, notices, cancellationToken).ConfigureAwait(false);
        }

        var graph = ProjectGraph.Load(roots);
        var testProjects = graph.Projects.Where(TestProjectRecognition.DeclaresTests).ToArray();

        if (testProjects.Length == 0)
        {
            return new AnalysisScopeResult(
                null,
                $"{target.Name} declares no tests, and analysis scope is the union of test-project "
                + "closures — so there is nothing here that could run a test. This is a "
                + "mis-invocation rather than an empty selection: reporting \"no tests affected\" "
                + "would be a lie a pipeline believes.",
                notices);
        }

        var projects = Union(graph, testProjects);

        return new AnalysisScopeResult(
            new AnalysisScope(projects, Sorted(testProjects), Expected(graph, projects, testProjects)),
            string.Empty,
            notices);
    }

    private static IReadOnlyList<ProjectFile> Union(ProjectGraph graph, IEnumerable<ProjectFile> testProjects)
    {
        var union = new Dictionary<string, ProjectFile>(Paths.Comparer);

        foreach (var project in testProjects.SelectMany(graph.ClosureOf))
        {
            union.TryAdd(project.Path, project);
        }

        return Sorted(union.Values);
    }

    /// <summary>
    /// Every <c>(project, target framework)</c> pair in scope, excluding projects that put no
    /// assembly where Reach will look: those every reference reaches with
    /// <c>ReferenceOutputAssembly=false</c> or as an analyser. Erring toward excluding is
    /// deliberate — a wrong inclusion becomes a false exit 3 in assembly discovery.
    /// </summary>
    private static IReadOnlyList<ExpectedAssemblyInstance> Expected(
        ProjectGraph graph,
        IEnumerable<ProjectFile> projects,
        IEnumerable<ProjectFile> testProjects)
    {
        var roots = testProjects
            .Select(project => project.Path)
            .ToHashSet(Paths.Comparer);

        return
        [
            .. projects
                .Where(project => roots.Contains(project.Path) || ProducesConsumableOutput(graph, project))
                .SelectMany(project => project.TargetFrameworks
                    .Select(framework => new ExpectedAssemblyInstance(project, framework)))
                .OrderBy(instance => instance.Project.Path, StringComparer.Ordinal)
                .ThenBy(instance => instance.TargetFramework, StringComparer.Ordinal)
        ];
    }

    private static bool ProducesConsumableOutput(ProjectGraph graph, ProjectFile project)
    {
        var references = graph.ReferencesTo(project);

        return references.Count == 0
            || references.Any(reference => reference.ReferenceOutputAssembly && !reference.IsAnalyzer);
    }

    /// <summary>
    /// A project named directly is legal: the solution only ever supplied the expected project
    /// list. It costs the check that turns a missing assembly into an error, so say so and
    /// continue rather than refusing.
    /// </summary>
    private static async Task NoteIfOutsideEverySolutionAsync(
        DiscoveredTarget target,
        List<Notice> notices,
        CancellationToken cancellationToken)
    {
        if (await IsListedInSomeSolutionAsync(target.Path, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        notices.Add(new Notice(
            NoticeCodes.ProjectOutsideEverySolution,
            NoticeKind.Scope,
            $"{target.Name} is not listed in any solution above it, so Reach has no expected "
            + "project list to check the build output against. A project whose assembly is "
            + "missing will be analysed as absent rather than reported as an error.",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["projects"] = new[] { target.Path } }));
    }

    private static async Task<bool> IsListedInSomeSolutionAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        for (var directory = Path.GetDirectoryName(projectPath);
            directory is not null;
            directory = Path.GetDirectoryName(directory))
        {
            foreach (var solution in Solutions(directory))
            {
                var listed = await SolutionReader
                    .ReadProjectPathsAsync(solution, cancellationToken)
                    .ConfigureAwait(false);

                if (listed.Any(path => Paths.Same(path, projectPath)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> Solutions(string directory)
    {
        try
        {
            return Directory
                .EnumerateFiles(directory)
                .Where(file => Path.GetExtension(file) is ".sln" or ".slnx")
                .ToArray();
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static IReadOnlyList<ProjectFile> Sorted(IEnumerable<ProjectFile> projects) =>
        [.. projects.OrderBy(project => project.Path, StringComparer.Ordinal)];
}
