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
using Reach.Rendering;
using Reach.Reporting;
using Reach.Selection;

namespace Reach;

/// <summary>What one <c>reach select</c> run concluded.</summary>
/// <param name="ExitCode">
/// Non-zero means <em>do not trust my answer</em>. That is what makes the pipeline rule
/// operational: if Reach exits non-zero, run the whole suite or stop the build.
/// </param>
/// <param name="Message">What went wrong. Empty when the run succeeded.</param>
/// <param name="WritesReport">
/// False only for a usage error, whose arguments were never valid enough to establish where to
/// write one. Every other outcome, failure included, carries a report — a tool that writes
/// nothing when it fails is one you debug by re-running it with more flags, on CI.
/// </param>
internal sealed record SelectRun(
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
        PhaseTimings timings,
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

        var scope = await timings
            .MeasureAsync("scope", () => AnalysisScopeResolver.ResolveAsync(target, cancellationToken))
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

        var baseline = await timings
            .MeasureAsync(
                "baseline",
                () => new BaselineResolver(git).ResolveAsync(request.Base, environment, cancellationToken))
            .ConfigureAwait(false);

        notices.AddRange(baseline.Notices);

        if (!baseline.Resolved)
        {
            return new SelectRun(ExitCode.BaselineUnresolvable, baseline.Message, notices);
        }

        var root = await git.RepositoryRootAsync(cancellationToken).ConfigureAwait(false);
        var head = await git.RevParseAsync("HEAD", cancellationToken).ConfigureAwait(false);

        var changes = await timings
            .MeasureAsync(
                "changes",
                () => new ChangedSetBuilder(git, root.Value, scope.Scope!)
                    .BuildAsync(baseline.Baseline!.Sha, cancellationToken))
            .ConfigureAwait(false);

        notices.AddRange(changes.Notices);

        var build = await timings
            .MeasureAsync(
                "build",
                () => new BuildAdapter(processRunner).BuildAsync(target, request, cancellationToken))
            .ConfigureAwait(false);

        var partial = new SelectRun(
            ExitCode.Success,
            string.Empty,
            notices,
            scope.Scope,
            baseline.Baseline,
            head.Succeeded ? head.Value : null,
            changes,
            RepositoryRoot: root.Succeeded ? root.Value : null);

        if (!build.Succeeded)
        {
            // No degraded mode: a failed build already fails the pipeline, so a clever
            // selection over a broken tree has no consumer.
            return partial with
            {
                ExitCode = ExitCode.BuildFailed,
                Message = "The build failed, so Reach selected nothing:\n\n" + build.Output,
            };
        }

        var assemblies = timings.Measure(
            "discovery",
            () => AssemblyDiscovery.Discover(scope.Scope!, target.Directory, root.Value, LayoutHints.From(request)));

        if (!assemblies.Succeeded)
        {
            return partial with { ExitCode = assemblies.ExitCode, Message = assemblies.Message };
        }

        // In both modes, and the mode that trusts the caller more is the mode that detects
        // more: default mode has just recompiled the changed file, so its checksums agree.
        var visibility = await SourceVisibility
            .ReadAsync(git, root.Value, cancellationToken)
            .ConfigureAwait(false);

        var correspondence = timings.Measure(
            "correspondence",
            () => Correspondence.Verify(assemblies.Instances, visibility));

        notices.AddRange(correspondence.Notices);
        partial = partial with { Assemblies = assemblies.Instances, Correspondence = correspondence };

        if (correspondence.Verdict == CorrespondenceVerdict.Failed)
        {
            return partial with { ExitCode = correspondence.ExitCode, Message = correspondence.Message };
        }

        using var open = OpenAssemblies.Open(assemblies.Instances);

        var graph = timings.Measure("graph", () => CallGraphBuilder.Build(open.Assemblies));
        notices.AddRange(graph.Notices);

        var roots = timings.Measure(
            "join",
            () => new SpanJoin(open.Assemblies, root.Value).ResolveAll(changes.Members));

        var selection = timings.Measure(
            "selection",
            () => Selector.Select(
                graph.Graph,
                open.Assemblies,
                assemblies.Instances,
                scope.Scope!,
                changes,
                roots,
                request.Paths,
                new TierLadder(scope.Scope!, root.Value)));

        notices.AddRange(selection.Notices);

        var rendered = timings.Measure(
            "rendering",
            () => new Renderer(
                    assemblies.Instances,
                    request,
                    ReachDirectory.For(request.WorkingDirectory, request.ReportDirectory))
                .Render(selection));

        notices.AddRange(rendered.Notices);

        if (TestRunnerConfiguration.Check(
                rendered.Entries
                    .Where(entry => TestRunnerConfiguration.Needs(entry.Selection.Dialect))
                    .Select(entry => entry.Selection.Project.Name),
                root.Value)
            is { } misconfigured)
        {
            // The rendering is correct; the repository is misconfigured. Reach says so and
            // does not stop.
            notices.Add(misconfigured);
        }

        // An empty selection exits 0. Non-zero means "do not trust my answer", which is what
        // makes the pipeline rule one line of guidance rather than a paragraph.
        return partial with
        {
            Graph = graph,
            Roots = roots,
            Selection = selection,
            Rendered = rendered,
        };
    }

    private static SelectRun Usage(string message) =>
        new(ExitCode.UsageError, message, [], WritesReport: false);
}
