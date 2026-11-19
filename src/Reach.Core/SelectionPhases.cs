using Reach.Assemblies;
using Reach.Changes;
using Reach.Graph;
using Reach.Join;
using Reach.Output;
using Reach.Rendering;
using Reach.Selection;

namespace Reach;

/// <summary>
/// The phases that run over the built output: finding the assemblies and proving they were
/// built from the working tree, then the call graph, the join, the selection and its rendering.
/// </summary>
internal static class SelectionPhases
{
    /// <summary>
    /// Completes a run the change phases left successful.
    /// </summary>
    internal static async Task<SelectRun> RunAsync(PipelineRun run, SelectRun partial)
    {
        var repositoryRoot = ChangePhases.WorkingRoot(partial.RepositoryRoot);

        var assemblies = run.Timings.Measure(
            "discovery",
            () => AssemblyDiscovery.Discover(
                run.Scope,
                run.Target.Directory,
                repositoryRoot,
                LayoutHints.From(run.Request)));

        if (!assemblies.Succeeded)
        {
            return partial with { ExitCode = assemblies.ExitCode, Message = assemblies.Message };
        }

        // In both modes, and the mode that trusts the caller more is the mode that detects
        // more: default mode has just recompiled the changed file, so its checksums agree.
        var visibility = await SourceVisibility
            .ReadAsync(run.Git, repositoryRoot, run.CancellationToken)
            .ConfigureAwait(false);

        var correspondence = run.Timings.Measure(
            "correspondence",
            () => Correspondence.Verify(assemblies.Instances, visibility));

        run.Notices.AddRange(correspondence.Notices);
        partial = partial with { Assemblies = assemblies.Instances, Correspondence = correspondence };

        if (correspondence.Verdict == CorrespondenceVerdict.Failed)
        {
            return partial with { ExitCode = correspondence.ExitCode, Message = correspondence.Message };
        }

        return Select(run, partial, assemblies.Instances, repositoryRoot);
    }

    private static SelectRun Select(
        PipelineRun run,
        SelectRun partial,
        IReadOnlyList<AssemblyInstance> instances,
        string repositoryRoot)
    {
        var (timings, notices) = (run.Timings, run.Notices);

        using var open = OpenAssemblies.Open(instances);

        var graph = timings.Measure("graph", () => CallGraphBuilder.Build(open.Assemblies));
        notices.AddRange(graph.Notices);

        var roots = timings.Measure(
            "join",
            () => new SpanJoin(open.Assemblies, repositoryRoot).ResolveAll(partial.Changes!.Members));

        var selection = timings.Measure(
            "selection",
            () => Selector.Select(
                graph,
                instances,
                run.Scope,
                partial.Changes!,
                roots,
                run.Request.Paths,
                new TierLadder(run.Scope, repositoryRoot)));

        notices.AddRange(selection.Notices);

        var rendered = timings.Measure(
            "rendering",
            () => new Renderer(
                    instances,
                    run.Request,
                    ReachDirectory.For(run.Request.WorkingDirectory, run.Request.ReportDirectory))
                .Render(selection));

        notices.AddRange(rendered.Notices);

        if (TestRunnerConfiguration.Check(
                rendered.Entries
                    .Where(entry => TestRunnerConfiguration.Needs(entry.Selection.Dialect))
                    .Select(entry => entry.Selection.Project.Name),
                repositoryRoot)
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
}
