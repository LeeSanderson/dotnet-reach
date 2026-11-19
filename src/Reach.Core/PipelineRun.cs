using Reach.Git;
using Reach.Projects;
using Reach.Reporting;

namespace Reach;

/// <summary>
/// One run past its usage checks: what was asked, against which target and scope, and
/// everything the phases have disclosed so far. The phases share it, and every result the run
/// ends in carries its one notice list.
/// </summary>
internal sealed class PipelineRun(
    SelectRequest request,
    EnvironmentLookup environment,
    PhaseTimings timings,
    DiscoveredTarget target,
    AnalysisScopeResult scope,
    GitAdapter git,
    CancellationToken cancellationToken)
{
    internal SelectRequest Request { get; } = request;

    internal EnvironmentLookup Environment { get; } = environment;

    internal PhaseTimings Timings { get; } = timings;

    internal DiscoveredTarget Target { get; } = target;

    internal AnalysisScope Scope { get; } = scope.Scope!;

    internal GitAdapter Git { get; } = git;

    internal CancellationToken CancellationToken { get; } = cancellationToken;

    /// <summary>
    /// Every notice so far, starting with scope resolution's. Each phase adds its own.
    /// </summary>
    internal List<Notice> Notices { get; } = [.. scope.Notices];
}
