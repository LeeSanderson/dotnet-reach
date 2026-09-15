using Reach.Git;
using Reach.Reporting;

namespace Reach.Baselines;

/// <summary>
/// Turns "no arguments in a CI job" into a baseline SHA, or into exit 4 with a message that
/// fixes the problem in one line.
/// </summary>
internal sealed class BaselineResolver(GitAdapter git)
{
    /// <summary>
    /// The detection ladder. First hit wins. Every entry is a <em>target</em> branch: the
    /// matching source-branch variables are the one detection bug that under-selects rather
    /// than over-selects, because a source branch resolves at or ahead of <c>HEAD</c> and
    /// produces an empty change set and a green pipeline.
    /// </summary>
    /// <remarks>
    /// TeamCity is deliberately absent: <c>teamcity.pullRequest.target.branch</c> is a
    /// configuration parameter, and TeamCity passes only <c>env.</c>-prefixed parameters to
    /// the build process. Its documentation shows <c>--base</c> explicitly.
    /// </remarks>
    private static readonly (string Variable, BaselineOrigin Origin)[] Ladder =
    [
        ("GITHUB_BASE_REF", BaselineOrigin.GitHubActions),
        ("SYSTEM_PULLREQUEST_TARGETBRANCH", BaselineOrigin.AzureDevOps),
        ("CI_MERGE_REQUEST_TARGET_BRANCH_NAME", BaselineOrigin.GitLabCi),
        ("BITBUCKET_PR_DESTINATION_BRANCH", BaselineOrigin.BitbucketPipelines),
        ("CHANGE_TARGET", BaselineOrigin.Jenkins),
    ];

    /// <summary>
    /// Rung 6 probes for concrete refs rather than reading <c>refs/remotes/origin/HEAD</c>,
    /// which does not exist after a CI checkout at any fetch depth: neither GitHub Actions
    /// nor the Azure Pipelines agent runs <c>git clone</c>, and only <c>clone</c> creates it.
    /// </summary>
    private static readonly string[] LocalRemoteBranches = ["origin/main", "origin/master"];

    internal async Task<BaselineResolution> ResolveAsync(
        string? baseReference,
        EnvironmentLookup environment,
        CancellationToken cancellationToken = default)
    {
        var notices = new List<Notice>();

        // First, because a shallow clone makes every later answer a truncated one, and
        // because it is what the likeliest first pipeline run actually hits.
        if (await IsShallowCloneAsync(cancellationToken).ConfigureAwait(false))
        {
            return Unresolvable(
                "the repository is a shallow clone, so there is no history behind HEAD to "
                + "take a merge-base in",
                environment,
                notices);
        }

        var (reference, origin, variable) =
            await DetectAsync(baseReference, environment, cancellationToken).ConfigureAwait(false);

        if (reference is null)
        {
            return Unresolvable(
                "no target branch was given with --base and none could be detected from the "
                + "environment",
                environment,
                notices);
        }

        var mergeBase = await git.MergeBaseAsync(reference, cancellationToken).ConfigureAwait(false);

        if (!mergeBase.Succeeded || mergeBase.Value.Length == 0)
        {
            return Unresolvable(
                $"git found no common commit between HEAD and '{reference}'",
                environment,
                notices);
        }

        var sha = mergeBase.Value;
        var head = await git.RevParseAsync("HEAD", cancellationToken).ConfigureAwait(false);
        var isHead = head.Succeeded && string.Equals(head.Value, sha, StringComparison.Ordinal);

        notices.Add(Notice.Environment(
            NoticeCodes.BaselineResolved,
            $"Baseline {sha} resolved as merge-base(HEAD, '{reference}'), {Describe(origin, variable)}.",
            ("sha", sha),
            ("reference", reference),
            ("origin", origin.ToString()),
            ("variable", variable)));

        if (isHead)
        {
            // The empty-change-set trap *is* a baseline that quietly resolved to HEAD, so
            // this is said whether or not the change set turns out empty.
            notices.Add(Notice.Environment(
                NoticeCodes.BaselineIsHead,
                $"The baseline is HEAD ({sha}), so only uncommitted work can appear as a change.",
                ("sha", sha)));
        }

        return BaselineResolution.From(new Baseline(sha, reference, origin, isHead), notices);
    }

    private async Task<bool> IsShallowCloneAsync(CancellationToken cancellationToken)
    {
        var result = await git.IsShallowCloneAsync(cancellationToken).ConfigureAwait(false);

        // A failed probe is treated as shallow. Exit 4 with a message beats a baseline
        // resolved against history Reach could not confirm is there.
        return !result.Succeeded
            || string.Equals(result.Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string? Reference, BaselineOrigin Origin, string? Variable)> DetectAsync(
        string? baseReference,
        EnvironmentLookup environment,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(baseReference))
        {
            return (baseReference, BaselineOrigin.Option, null);
        }

        foreach (var (variable, origin) in Ladder)
        {
            var value = environment(variable);

            if (!string.IsNullOrEmpty(value))
            {
                return (StripRefsHeads(value), origin, variable);
            }
        }

        return (await SoleLocalRemoteBranchAsync(cancellationToken).ConfigureAwait(false),
            BaselineOrigin.LocalRemoteBranch,
            null);
    }

    /// <summary>
    /// Azure DevOps' format varies by repository provider — <c>refs/heads/main</c> for Azure
    /// Repos, bare <c>main</c> for a GitHub-hosted repository. Applied to every rung, because
    /// a bare name is unaffected and a prefixed one would otherwise never resolve.
    /// </summary>
    private static string StripRefsHeads(string reference) =>
        reference.StartsWith("refs/heads/", StringComparison.Ordinal)
            ? reference["refs/heads/".Length..]
            : reference;

    /// <summary>Exactly one, because two is ambiguous and picking either could be wrong.</summary>
    private async Task<string?> SoleLocalRemoteBranchAsync(CancellationToken cancellationToken)
    {
        string? found = null;

        foreach (var branch in LocalRemoteBranches)
        {
            var result = await git.RevParseAsync(branch, cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = branch;
        }

        return found;
    }

    private static BaselineResolution Unresolvable(
        string problem,
        EnvironmentLookup environment,
        List<Notice> notices)
    {
        var message = CheckoutGuidance.Message(problem, environment);

        notices.Add(Notice.Environment(NoticeCodes.BaselineUnresolvable, message, ("problem", problem)));

        return BaselineResolution.Unresolvable(message, notices);
    }

    private static string Describe(BaselineOrigin origin, string? variable) => origin switch
    {
        BaselineOrigin.Option => "given with --base",
        BaselineOrigin.LocalRemoteBranch => "the only local remote branch found",
        _ => $"detected from {variable}",
    };
}
