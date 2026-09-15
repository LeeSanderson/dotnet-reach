namespace Reach.Baselines;

/// <summary>
/// The literal line to add, per CI provider. Exit 4 is a product surface, not an error string:
/// <c>actions/checkout</c> defaults to <c>fetch-depth: 1</c> and on a pull-request build
/// fetches one refspec, so Reach does not work under default CI settings and this is the
/// likeliest result of anyone's first pipeline run. "Baseline unresolvable" on its own would
/// convert a one-line fix into a support question.
/// </summary>
internal static class CheckoutGuidance
{
    /// <summary>The whole exit-4 message: what went wrong, then how to fix it here.</summary>
    internal static string Message(string problem, EnvironmentLookup environment) =>
        $"""
        Reach could not resolve a baseline: {problem}.

        {FixFor(environment)}

        Or name the branch this work merges into explicitly:

            reach select <SOLUTION|PROJECT> --base <ref>
        """;

    private static string FixFor(EnvironmentLookup environment)
    {
        if (IsSet(environment, "GITHUB_ACTIONS", "GITHUB_BASE_REF", "GITHUB_WORKFLOW"))
        {
            return """
                   actions/checkout fetches a single commit by default, which leaves no history
                   for git merge-base to walk. Fetch the whole history:

                       - uses: actions/checkout@v5
                         with:
                           fetch-depth: 0
                   """;
        }

        if (IsSet(environment, "TF_BUILD", "SYSTEM_TEAMPROJECTID", "SYSTEM_PULLREQUEST_TARGETBRANCH"))
        {
            // One hyphen apart from the GitHub spelling, and the wrong one silently does
            // nothing on this agent.
            return """
                   The Azure Pipelines agent fetches a single commit by default, which leaves no
                   history for git merge-base to walk. Fetch the whole history:

                       steps:
                         - checkout: self
                           fetchDepth: 0
                   """;
        }

        if (IsSet(environment, "GITLAB_CI", "CI_MERGE_REQUEST_TARGET_BRANCH_NAME"))
        {
            return """
                   GitLab CI clones to a fixed depth by default, which leaves no history for
                   git merge-base to walk. Fetch the whole history:

                       variables:
                         GIT_DEPTH: 0
                   """;
        }

        if (IsSet(environment, "BITBUCKET_BUILD_NUMBER", "BITBUCKET_PR_DESTINATION_BRANCH"))
        {
            return """
                   Bitbucket Pipelines clones to a fixed depth by default, which leaves no
                   history for git merge-base to walk. Fetch the whole history:

                       clone:
                         depth: full
                   """;
        }

        if (IsSet(environment, "JENKINS_URL", "CHANGE_TARGET"))
        {
            return """
                   A shallow checkout leaves no history for git merge-base to walk. Turn off
                   'Shallow clone' in the job's Git behaviours, or set its depth to 0.
                   """;
        }

        return """
               A shallow or partial checkout leaves no history for git merge-base to walk.
               Deepen it with `git fetch --unshallow`.
               """;
    }

    private static bool IsSet(EnvironmentLookup environment, params string[] names) =>
        names.Any(name => !string.IsNullOrEmpty(environment(name)));
}
