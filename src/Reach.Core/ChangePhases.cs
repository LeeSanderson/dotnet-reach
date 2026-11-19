using Reach.Baselines;
using Reach.Build;
using Reach.Changes;
using Reach.Processes;

namespace Reach;

/// <summary>
/// The phases that establish what changed and build it: the baseline, the changed set, and the
/// build. Their result is the run so far, which already carries a report.
/// </summary>
internal static class ChangePhases
{
    /// <summary>
    /// The run so far, and the working tree's root as git reported it. A run whose exit code is
    /// not success stops here.
    /// </summary>
    internal static async Task<(SelectRun Run, string RepositoryRoot)> RunAsync(
        PipelineRun run,
        IProcessRunner processRunner)
    {
        var (git, timings, cancellationToken) = (run.Git, run.Timings, run.CancellationToken);

        var baseline = await timings
            .MeasureAsync(
                "baseline",
                () => new BaselineResolver(git).ResolveAsync(run.Request.Base, run.Environment, cancellationToken))
            .ConfigureAwait(false);

        run.Notices.AddRange(baseline.Notices);

        if (!baseline.Resolved)
        {
            return (new SelectRun(ExitCode.BaselineUnresolvable, baseline.Message, run.Notices), string.Empty);
        }

        var root = await git.RepositoryRootAsync(cancellationToken).ConfigureAwait(false);
        var head = await git.RevParseAsync("HEAD", cancellationToken).ConfigureAwait(false);

        var changes = await timings
            .MeasureAsync(
                "changes",
                () => new ChangedSetBuilder(git, root.Value, run.Scope)
                    .BuildAsync(baseline.Baseline!.Sha, cancellationToken))
            .ConfigureAwait(false);

        run.Notices.AddRange(changes.Notices);

        var build = await timings
            .MeasureAsync(
                "build",
                () => new BuildAdapter(processRunner).BuildAsync(run.Target, run.Request, cancellationToken))
            .ConfigureAwait(false);

        var partial = new SelectRun(
            ExitCode.Success,
            string.Empty,
            run.Notices,
            run.Scope,
            baseline.Baseline,
            head.Succeeded ? head.Value : null,
            changes,
            RepositoryRoot: root.Succeeded ? root.Value : null);

        if (!build.Succeeded)
        {
            // No degraded mode: a failed build already fails the pipeline, so a clever
            // selection over a broken tree has no consumer.
            partial = partial with
            {
                ExitCode = ExitCode.BuildFailed,
                Message = "The build failed, so Reach selected nothing:\n\n" + build.Output,
            };
        }

        return (partial, root.Value);
    }
}
