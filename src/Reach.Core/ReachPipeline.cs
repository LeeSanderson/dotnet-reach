using Reach.Assemblies;
using Reach.Baselines;
using Reach.Build;
using Reach.Changes;
using Reach.Git;
using Reach.Graph;
using Reach.Join;
using Reach.Output;
using Reach.Processes;
using Reach.Projects;
using Reach.Reporting;
using Reach.Selection;

namespace Reach;

/// <summary>What one <c>reach select</c> run concluded.</summary>
/// <param name="ExitCode">
/// Non-zero means <em>do not trust my answer</em>. That is what makes the pipeline rule
/// operational: if Reach exits non-zero, run the whole suite or stop the build.
/// </param>
/// <param name="Message">What to tell the caller. Empty when the run succeeded.</param>
internal sealed record SelectRun(
    ExitCode ExitCode,
    string Message,
    IReadOnlyList<Notice> Notices,
    AnalysisScope? Scope = null,
    Baseline? Baseline = null,
    ChangedSet? Changes = null,
    IReadOnlyList<AssemblyInstance>? Assemblies = null,
    CorrespondenceResult? Correspondence = null,
    CallGraphResult? Graph = null,
    IReadOnlyList<JoinResult>? Roots = null,
    SelectionResult? Selection = null);

/// <summary>
/// The phases of one run, in order. Usage errors come first and deliberately: exit 1 is the
/// one case that writes no report, so nothing may touch the directory Reach owns before the
/// invocation has been shown to be a usable one.
/// </summary>
internal sealed class ReachPipeline(IProcessRunner processRunner)
{
    internal async Task<SelectRun> SelectAsync(
        SelectRequest request,
        EnvironmentLookup environment,
        CancellationToken cancellationToken = default)
    {
        var refusal = ForwardedBuildArguments.Refuse(request.ForwardedBuildArguments);

        if (refusal is not null)
        {
            return Usage(refusal);
        }

        var discovery = TargetDiscovery.Discover(request.Target ?? request.WorkingDirectory);

        if (!discovery.Discovered)
        {
            return Usage(discovery.Message);
        }

        var target = discovery.Target!;

        var scope = await AnalysisScopeResolver
            .ResolveAsync(target, cancellationToken)
            .ConfigureAwait(false);

        if (!scope.Resolved)
        {
            return Usage(scope.Message);
        }

        // Past here the invocation is a usable one, so the directory Reach owns comes into
        // existence and every later outcome carries a report.
        ReachDirectory.For(request.WorkingDirectory, request.ReportDirectory).EnsureCreated();

        var notices = new List<Notice>(scope.Notices);
        var git = new GitAdapter(processRunner, target.Directory);

        var baseline = await new BaselineResolver(git)
            .ResolveAsync(request.Base, environment, cancellationToken)
            .ConfigureAwait(false);

        notices.AddRange(baseline.Notices);

        if (!baseline.Resolved)
        {
            return new SelectRun(ExitCode.BaselineUnresolvable, baseline.Message, notices);
        }

        var root = await git.RepositoryRootAsync(cancellationToken).ConfigureAwait(false);

        var changes = await new ChangedSetBuilder(git, root.Value, scope.Scope!)
            .BuildAsync(baseline.Baseline!.Sha, cancellationToken)
            .ConfigureAwait(false);

        notices.AddRange(changes.Notices);

        var build = await new BuildAdapter(processRunner)
            .BuildAsync(target, request, cancellationToken)
            .ConfigureAwait(false);

        if (!build.Succeeded)
        {
            // No degraded mode: a failed build already fails the pipeline, so a clever
            // selection over a broken tree has no consumer.
            return new SelectRun(
                ExitCode.BuildFailed,
                "The build failed, so Reach selected nothing:\n\n" + build.Output,
                notices);
        }

        var assemblies = AssemblyDiscovery.Discover(
            scope.Scope!,
            target.Directory,
            root.Value,
            LayoutHints.From(request));

        if (!assemblies.Succeeded)
        {
            return new SelectRun(assemblies.ExitCode, assemblies.Message, notices);
        }

        // In both modes, and the mode that trusts the caller more is the mode that detects
        // more: default mode has just recompiled the changed file, so its checksums agree.
        var visibility = await SourceVisibility
            .ReadAsync(git, root.Value, cancellationToken)
            .ConfigureAwait(false);

        var correspondence = Correspondence.Verify(assemblies.Instances, visibility);
        notices.AddRange(correspondence.Notices);

        if (correspondence.Verdict == CorrespondenceVerdict.Failed)
        {
            return new SelectRun(correspondence.ExitCode, correspondence.Message, notices);
        }

        using var open = OpenAssemblies.Open(assemblies.Instances);

        var graph = CallGraphBuilder.Build(open.Assemblies);
        notices.AddRange(graph.Notices);

        var roots = new SpanJoin(open.Assemblies, root.Value).ResolveAll(changes.Members);

        var selection = Selector.Select(
            graph.Graph,
            open.Assemblies,
            assemblies.Instances,
            scope.Scope!,
            changes,
            roots,
            request.Paths);

        notices.AddRange(selection.Notices);

        return new SelectRun(
            ExitCode.InternalError,
            $"Reach selected {selection.SelectedCount} test(s) from {changes.Members.Count} changed "
            + $"declaration(s) over {assemblies.Instances.Count} assembly instance(s) — but "
            + "rendering, delivery and the report are not implemented yet.",
            notices,
            scope.Scope,
            baseline.Baseline,
            changes,
            assemblies.Instances,
            correspondence,
            graph,
            roots,
            selection);
    }

    private static SelectRun Usage(string message) => new(ExitCode.UsageError, message, []);
}
