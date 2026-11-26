using Reach.Reporting;

namespace Reach.Projects;

/// <summary>
/// The assemblies Reach reads to build the call graph: the union of every test project's
/// transitive project closure. Code outside every test closure cannot execute in any test
/// process, so a change to it cannot affect a test.
/// </summary>
/// <param name="Projects">The union of the closures. Never narrowed to the change's dependents.</param>
/// <param name="TestProjects">The roots the union was taken from.</param>
/// <param name="ExpectedAssemblies">
/// Every <c>(project, target framework)</c> pair in scope that is expected to put an assembly
/// where Reach will look. A missing member of this set is exit 3, so a wrong exclusion here is
/// a false error there.
/// </param>
public sealed record AnalysisScope(
    IReadOnlyList<ProjectFile> Projects,
    IReadOnlyList<ProjectFile> TestProjects,
    IReadOnlyList<ExpectedAssemblyInstance> ExpectedAssemblies)
{
    /// <summary>
    /// The in-scope project with the nearest ancestor directory, which reproduces the SDK's
    /// default <c>**/*.cs</c> globbing without running MSBuild. Errs over — a path excluded by
    /// <c>&lt;Compile Remove&gt;</c> over-selects — and is imprecise only for linked files pulled
    /// in from outside the project directory.
    /// </summary>
    /// <returns>Null when the path is outside the analysis scope: no in-scope project contains it.</returns>
    internal ProjectFile? ProjectContaining(string absolutePath) =>
        Projects
            .Where(project => Paths.IsUnder(absolutePath, project.Directory))
            .OrderByDescending(project => project.Directory.Length)
            .FirstOrDefault();

    /// <summary>
    /// <see cref="ProjectContaining(string)"/> for a path relative to the repository root, the
    /// form git reports a change in.
    /// </summary>
    internal ProjectFile? ProjectContaining(string repositoryRoot, string repositoryRelativePath) =>
        ProjectContaining(Paths.Normalise(Path.Combine(repositoryRoot, repositoryRelativePath)));
}

/// <summary>
/// One project compiled for one target framework — the unit Reach actually analyses.
/// </summary>
public sealed record ExpectedAssemblyInstance(ProjectFile Project, string TargetFramework)
{
    public override string ToString() => $"{Project.AssemblyName} ({TargetFramework})";
}

/// <summary>
/// What scope resolution concluded. A target with no test project in its closure is a
/// mis-invocation rather than <c>nothing-selected</c>: reporting "no tests affected" would be
/// a lie a pipeline believes.
/// </summary>
internal sealed record AnalysisScopeResult(
    AnalysisScope? Scope,
    string Message,
    IReadOnlyList<Notice> Notices)
{
    internal bool Resolved => Scope is not null;

    internal ExitCode ExitCode => Resolved ? ExitCode.Success : ExitCode.UsageError;
}
