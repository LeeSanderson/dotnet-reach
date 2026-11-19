using Reach.Assemblies;
using Reach.Graph;
using Reach.Projects;
using Reach.Reporting;

namespace Reach.Selection;

/// <summary>
/// Every test method in every in-scope test project, not only the selected ones: the totals, the
/// rendered-match computation and the over-selection measurement all read the list.
/// </summary>
internal static class TestEnumeration
{
    /// <summary>
    /// One entry per test project's assembly instance. A project whose test framework Reach does
    /// not recognise has no framework and no tests, and is disclosed in one notice.
    /// </summary>
    internal static IReadOnlyList<EnumeratedProject> Enumerate(
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
}

/// <summary>
/// One test project's assembly instance and the test methods it declares.
/// </summary>
/// <param name="Project">The test project.</param>
/// <param name="TargetFramework">The target framework this instance was built for.</param>
/// <param name="Framework">The test framework detected in it, or null when Reach does not recognise one.</param>
/// <param name="Tests">Every test method it declares; empty when the framework is unrecognised.</param>
internal sealed record EnumeratedProject(
    ProjectFile Project,
    string TargetFramework,
    TestFramework? Framework,
    IReadOnlyList<TestMethod> Tests);
