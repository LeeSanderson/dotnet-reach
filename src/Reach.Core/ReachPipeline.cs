using Reach.Assemblies;
using Reach.Baselines;
using Reach.Changes;
using Reach.Git;
using Reach.Graph;
using Reach.Join;
using Reach.Output;
using Reach.Processes;
using Reach.Projects;
using Reach.Rendering;
using Reach.Reporting;
using Reach.Selection;

namespace Reach;

/// <summary>
/// The phases of one run, in order. Usage errors come first and deliberately: exit 1 is the
/// one case that writes no report, so nothing may touch the directory Reach owns before the
/// invocation has been shown to be a usable one.
/// </summary>
public sealed class ReachPipeline(IProcessRunner processRunner)
{
    public async Task<SelectRun> SelectAsync(
        SelectRequest request,
        EnvironmentLookup environment,
        PhaseTimings timings,
        CancellationToken cancellationToken)
    {
        var check = await CheckUsageAsync(request, timings, cancellationToken).ConfigureAwait(false);

        if (check.Usable is not { } usable)
        {
            return Usage(check.Refusal);
        }

        // Past here the invocation is a usable one, so the directory Reach owns comes into
        // existence and every later outcome carries a report.
        ReachDirectory.For(request.WorkingDirectory, request.ReportDirectory).EnsureCreated();

        var run = new PipelineRun(
            request,
            environment,
            timings,
            usable,
            new GitAdapter(processRunner, usable.Target.Directory),
            cancellationToken);

        var partial = await ChangePhases.RunAsync(run, processRunner).ConfigureAwait(false);

        if (partial.ExitCode != ExitCode.Success)
        {
            return partial;
        }

        return await SelectionPhases.RunAsync(run, partial).ConfigureAwait(false);
    }

    /// <summary>
    /// The checks that can still end the run as a usage error: the forwarded build arguments,
    /// the target, and its analysis scope.
    /// </summary>
    private static async Task<UsageCheck> CheckUsageAsync(
        SelectRequest request,
        PhaseTimings timings,
        CancellationToken cancellationToken)
    {
        if (ForwardedBuildArguments.Refuse(request.ForwardedBuildArguments) is { } refusal)
        {
            return UsageCheck.Refused(refusal);
        }

        var discovery = TargetDiscovery.Discover(request.Target ?? request.WorkingDirectory);

        if (discovery.Target is not { } target)
        {
            return UsageCheck.Refused(discovery.Message);
        }

        var scope = await timings
            .MeasureAsync("scope", () => AnalysisScopeResolver.ResolveAsync(target, cancellationToken))
            .ConfigureAwait(false);

        return scope.Scope is { } resolved
            ? UsageCheck.Passed(new UsableTarget(target, resolved, scope.Notices))
            : UsageCheck.Refused(scope.Message);
    }

    private static SelectRun Usage(string message) =>
        new(ExitCode.UsageError, message, [], WritesReport: false);
}

/// <summary>
/// What one <c>reach select</c> run concluded. Every phase's result is null when the run stopped
/// before that phase.
/// </summary>
/// <param name="ExitCode">
/// Non-zero means <em>do not trust my answer</em>. That is what makes the pipeline rule
/// operational: if Reach exits non-zero, run the whole suite or stop the build.
/// </param>
/// <param name="Message">What went wrong. Empty when the run succeeded.</param>
/// <param name="Notices">Everything the run had to disclose, from every phase it reached.</param>
/// <param name="Scope">The projects in scope and the assemblies they are expected to build.</param>
/// <param name="Baseline">The revision the change is measured from.</param>
/// <param name="Head">The commit <c>HEAD</c> resolved to.</param>
/// <param name="Changes">What changed since the baseline.</param>
/// <param name="Assemblies">The assembly instances found on disk.</param>
/// <param name="Correspondence">Whether those assemblies were built from the working tree.</param>
/// <param name="Graph">The call graph over them.</param>
/// <param name="Roots">Where each changed declaration joined the call graph.</param>
/// <param name="Selection">The tests the reverse walk reached.</param>
/// <param name="Rendered">The selection as argument vectors for the runner.</param>
/// <param name="RepositoryRoot">The working tree's root directory.</param>
/// <param name="WritesReport">
/// False only for a usage error, whose arguments were never valid enough to establish where to
/// write one. Every other outcome, failure included, carries a report — a tool that writes
/// nothing when it fails is one you debug by re-running it with more flags, on CI.
/// </param>
public sealed record SelectRun(
    ExitCode ExitCode,
    string Message,
    IReadOnlyList<Notice> Notices,
    AnalysisScope? Scope = null,
    Baseline? Baseline = null,
    string? Head = null,
    ChangedSet? Changes = null,
    IReadOnlyList<AssemblyInstance>? Assemblies = null,
    CorrespondenceResult? Correspondence = null,
    CallGraphResult? Graph = null,
    IReadOnlyList<JoinResult>? Roots = null,
    SelectionResult? Selection = null,
    RenderedSelection? Rendered = null,
    string? RepositoryRoot = null,
    bool WritesReport = true);
