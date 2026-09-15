using Reach.Reporting;

namespace Reach.Projects;

/// <summary>One project compiled for one target framework — the unit Reach actually analyses.</summary>
internal sealed record ExpectedAssemblyInstance(ProjectFile Project, string TargetFramework)
{
    public override string ToString() => $"{Project.AssemblyName} ({TargetFramework})";
}

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
internal sealed record AnalysisScope(
    IReadOnlyList<ProjectFile> Projects,
    IReadOnlyList<ProjectFile> TestProjects,
    IReadOnlyList<ExpectedAssemblyInstance> ExpectedAssemblies);

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
