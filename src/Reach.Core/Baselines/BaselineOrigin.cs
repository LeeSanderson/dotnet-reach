namespace Reach.Baselines;

/// <summary>
/// Which rung of the detection ladder produced the reference. Disclosed in the human summary
/// and in the report, because a wrong baseline is Reach's most dangerous failure and an audit
/// trail that cannot say what it audited will not catch one.
/// </summary>
internal enum BaselineOrigin
{
    /// <summary><c>--base</c>, which wins over every variable.</summary>
    Option,

    /// <summary><c>GITHUB_BASE_REF</c>.</summary>
    GitHubActions,

    /// <summary><c>SYSTEM_PULLREQUEST_TARGETBRANCH</c>.</summary>
    AzureDevOps,

    /// <summary><c>CI_MERGE_REQUEST_TARGET_BRANCH_NAME</c>.</summary>
    GitLabCi,

    /// <summary><c>BITBUCKET_PR_DESTINATION_BRANCH</c>.</summary>
    BitbucketPipelines,

    /// <summary><c>CHANGE_TARGET</c>.</summary>
    Jenkins,

    /// <summary>
    /// Exactly one of <c>origin/main</c> or <c>origin/master</c> present locally. A
    /// developer-machine convenience that never fires in CI.
    /// </summary>
    LocalRemoteBranch,
}
