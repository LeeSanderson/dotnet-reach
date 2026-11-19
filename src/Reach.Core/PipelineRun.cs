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
    UsableTarget usable,
    GitAdapter git,
    CancellationToken cancellationToken)
{
    internal SelectRequest Request { get; } = request;

    internal EnvironmentLookup Environment { get; } = environment;

    internal PhaseTimings Timings { get; } = timings;

    internal DiscoveredTarget Target { get; } = usable.Target;

    internal AnalysisScope Scope { get; } = usable.Scope;

    internal GitAdapter Git { get; } = git;

    internal CancellationToken CancellationToken { get; } = cancellationToken;

    /// <summary>
    /// Every notice so far, starting with scope resolution's. Each phase adds its own.
    /// </summary>
    internal List<Notice> Notices { get; } = [.. usable.ScopeNotices];
}

/// <summary>
/// A target that passed the usage checks, with the analysis scope it resolved to.
/// </summary>
/// <param name="Target">The solution or project the run is against.</param>
/// <param name="Scope">The projects in scope.</param>
/// <param name="ScopeNotices">What resolving the scope disclosed.</param>
internal sealed record UsableTarget(
    DiscoveredTarget Target,
    AnalysisScope Scope,
    IReadOnlyList<Notice> ScopeNotices);

/// <summary>
/// The usage checks' answer: a target the run can go on with, or why it cannot.
/// </summary>
/// <param name="Usable">The target and its scope. Null when the run is refused.</param>
/// <param name="Refusal">Why the run is refused. Empty when it is not.</param>
internal sealed record UsageCheck(UsableTarget? Usable, string Refusal)
{
    internal static UsageCheck Passed(UsableTarget usable) => new(usable, string.Empty);

    internal static UsageCheck Refused(string refusal) => new(null, refusal);
}
