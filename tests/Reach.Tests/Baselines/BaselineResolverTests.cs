using Reach.Baselines;
using Reach.Git;
using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Baselines;

/// <summary>
/// The detection ladder, driven entirely through the fake. A wrong baseline is Reach's most
/// dangerous failure, and every rung here is a rung someone's CI provider actually sits on.
/// </summary>
public class BaselineResolverTests
{
    private const string Head = "1111111111111111111111111111111111111111";
    private const string ForkPoint = "2222222222222222222222222222222222222222";

    private readonly ScriptedProcessRunner _runner = new();
    private readonly Dictionary<string, string?> _environment = new(StringComparer.Ordinal);

    public BaselineResolverTests()
    {
        // First, so the two specific rev-parse scripts below still win: every reference names
        // something unless a test says otherwise. Resolution verifies the detected reference
        // before taking a merge-base, because a CI checkout has no local branches.
        _runner.Succeeds($"{ForkPoint}\n", "rev-parse");
        _runner.Succeeds("false\n", "--is-shallow-repository");
        _runner.Succeeds($"{ForkPoint}\n", "merge-base");
        _runner.Succeeds($"{Head}\n", "rev-parse", "HEAD");
    }

    private Task<BaselineResolution> ResolveAsync(string? baseReference = null) =>
        new BaselineResolver(new GitAdapter(_runner, "/work"))
            .ResolveAsync(baseReference, Lookup, TestContext.Current.CancellationToken);

    private string? Lookup(string name) =>
        _environment.TryGetValue(name, out var value) ? value : null;

    // ---- The ladder -------------------------------------------------------------------

    [Fact]
    public async Task The_option_wins_over_every_variable()
    {
        _environment["GITHUB_BASE_REF"] = "main";

        var resolution = await ResolveAsync("release/1.0");

        AssertResolved(resolution, BaselineOrigin.Option, "release/1.0");
    }

    [Fact]
    public async Task Rung_1_is_GITHUB_BASE_REF()
    {
        _environment["GITHUB_BASE_REF"] = "main";

        AssertResolved(await ResolveAsync(), BaselineOrigin.GitHubActions, "main");
    }

    [Fact]
    public async Task Rung_2_is_the_Azure_DevOps_target_branch()
    {
        _environment["SYSTEM_PULLREQUEST_TARGETBRANCH"] = "refs/heads/develop";

        AssertResolved(await ResolveAsync(), BaselineOrigin.AzureDevOps, "develop");
    }

    [Fact]
    public async Task Rung_3_is_the_GitLab_merge_request_target()
    {
        _environment["CI_MERGE_REQUEST_TARGET_BRANCH_NAME"] = "main";

        AssertResolved(await ResolveAsync(), BaselineOrigin.GitLabCi, "main");
    }

    [Fact]
    public async Task Rung_4_is_the_Bitbucket_destination_branch()
    {
        _environment["BITBUCKET_PR_DESTINATION_BRANCH"] = "main";

        AssertResolved(await ResolveAsync(), BaselineOrigin.BitbucketPipelines, "main");
    }

    [Fact]
    public async Task Rung_5_is_the_Jenkins_change_target()
    {
        _environment["CHANGE_TARGET"] = "main";

        AssertResolved(await ResolveAsync(), BaselineOrigin.Jenkins, "main");
    }

    [Fact]
    public async Task Rung_6_probes_for_a_local_remote_branch_and_needs_exactly_one()
    {
        _runner.Succeeds("abc\n", "rev-parse", "origin/main");
        _runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/master");

        AssertResolved(await ResolveAsync(), BaselineOrigin.LocalRemoteBranch, "origin/main");
    }

    [Fact]
    public async Task Rung_6_declines_when_both_local_remote_branches_exist()
    {
        _runner.Succeeds("abc\n", "rev-parse", "origin/main");
        _runner.Succeeds("def\n", "rev-parse", "origin/master");

        AssertUnresolvable(await ResolveAsync());
    }

    [Fact]
    public async Task Rung_7_is_exit_4()
    {
        _runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/main");
        _runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/master");

        var resolution = await ResolveAsync();

        AssertUnresolvable(resolution);
        Assert.Equal(ExitCode.BaselineUnresolvable, resolution.ExitCode);
    }

    // ---- The spelling a provider hands out is not the one git can name ------------------

    [Fact]
    public async Task A_detected_branch_that_names_nothing_locally_falls_back_to_the_remote()
    {
        // The environment every rung exists for: actions/checkout fetches into
        // refs/remotes/origin/* and detaches HEAD, so `main` names nothing and `origin/main`
        // is the same branch. Without the fallback the whole ladder is exit 4 in CI.
        _runner.Fails(128, "fatal: ambiguous argument 'main'\n", "rev-parse", "main");
        _runner.Succeeds("abc\n", "rev-parse", "origin/main");

        _environment["GITHUB_BASE_REF"] = "main";

        AssertResolved(await ResolveAsync(), BaselineOrigin.GitHubActions, "origin/main");
    }

    [Fact]
    public async Task A_local_branch_wins_over_the_remote_one_of_the_same_name()
    {
        // A developer running --base main by hand means their branch, which can legitimately
        // sit behind or ahead of the remote's.
        _runner.Succeeds("abc\n", "rev-parse", "main");
        _runner.Succeeds("def\n", "rev-parse", "origin/main");

        AssertResolved(await ResolveAsync("main"), BaselineOrigin.Option, "main");
    }

    [Fact]
    public async Task A_branch_name_containing_a_slash_falls_back_too()
    {
        // release/1.0 and feature/a-b are ordinary branch names, not qualified references, and
        // they hit the CI checkout exactly as main does. Reserving the fallback for names
        // without a slash would have excluded the commonest naming convention there is.
        _runner.Fails(128, "fatal: ambiguous argument 'release/1.0'\n", "rev-parse", "release/1.0");
        _runner.Succeeds("abc\n", "rev-parse", "origin/release/1.0");

        _environment["SYSTEM_PULLREQUEST_TARGETBRANCH"] = "refs/heads/release/1.0";

        AssertResolved(await ResolveAsync(), BaselineOrigin.AzureDevOps, "origin/release/1.0");
    }

    [Fact]
    public async Task A_reference_neither_spelling_can_name_is_exit_4_saying_both()
    {
        _runner.Fails(128, "fatal: ambiguous argument\n", "rev-parse", "upstream/main");
        _runner.Fails(128, "fatal: ambiguous argument\n", "rev-parse", "origin/upstream/main");

        var resolution = await ResolveAsync("upstream/main");

        AssertUnresolvable(resolution);
        Assert.Contains("'upstream/main'", resolution.Message, StringComparison.Ordinal);
        Assert.Contains("'origin/upstream/main'", resolution.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_first_hit_wins()
    {
        _environment["GITHUB_BASE_REF"] = "from-github";
        _environment["CI_MERGE_REQUEST_TARGET_BRANCH_NAME"] = "from-gitlab";
        _environment["CHANGE_TARGET"] = "from-jenkins";

        AssertResolved(await ResolveAsync(), BaselineOrigin.GitHubActions, "from-github");
    }

    [Fact]
    public async Task An_empty_variable_is_not_a_hit()
    {
        _environment["GITHUB_BASE_REF"] = "";
        _environment["CHANGE_TARGET"] = "main";

        AssertResolved(await ResolveAsync(), BaselineOrigin.Jenkins, "main");
    }

    [Theory]
    [InlineData("refs/heads/main", "main")]
    [InlineData("main", "main")]
    [InlineData("refs/heads/feature/a-b", "feature/a-b")]
    public async Task Azures_two_formats_strip_to_the_same_branch_name(string variable, string expected)
    {
        // The format varies by repo provider: refs/heads/main for Azure Repos, bare main for
        // a GitHub-hosted repository behind an Azure pipeline.
        _environment["SYSTEM_PULLREQUEST_TARGETBRANCH"] = variable;

        AssertResolved(await ResolveAsync(), BaselineOrigin.AzureDevOps, expected);
    }

    // ---- The source-branch traps ------------------------------------------------------
    //
    // Picking a source branch is the one detection bug that under-selects rather than
    // over-selects: it resolves at or ahead of HEAD, giving an empty change set and a green
    // pipeline. Each gets its own named test so the failure names the variable.

    [Fact]
    public async Task GITHUB_HEAD_REF_is_never_read() => await AssertTrapAsync("GITHUB_HEAD_REF");

    [Fact]
    public async Task SYSTEM_PULLREQUEST_SOURCEBRANCH_is_never_read() =>
        await AssertTrapAsync("SYSTEM_PULLREQUEST_SOURCEBRANCH");

    [Fact]
    public async Task BUILD_SOURCEBRANCH_is_never_read() => await AssertTrapAsync("BUILD_SOURCEBRANCH");

    [Fact]
    public async Task CI_MERGE_REQUEST_SOURCE_BRANCH_NAME_is_never_read() =>
        await AssertTrapAsync("CI_MERGE_REQUEST_SOURCE_BRANCH_NAME");

    [Fact]
    public async Task BITBUCKET_BRANCH_is_never_read() => await AssertTrapAsync("BITBUCKET_BRANCH");

    [Fact]
    public async Task CHANGE_BRANCH_is_never_read() => await AssertTrapAsync("CHANGE_BRANCH");

    // ---- Shallow clones and the exit-4 message ----------------------------------------

    [Fact]
    public async Task A_shallow_clone_is_exit_4_before_anything_else_is_tried()
    {
        _runner.Returns(new(0, "true\n", ""), "--is-shallow-repository");
        _environment["GITHUB_BASE_REF"] = "main";

        var resolution = await ResolveAsync();

        AssertUnresolvable(resolution);
        Assert.Contains("shallow", resolution.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exit_4_under_GitHub_Actions_prints_the_line_to_add()
    {
        _runner.Returns(new(0, "true\n", ""), "--is-shallow-repository");
        _environment["GITHUB_ACTIONS"] = "true";

        var resolution = await ResolveAsync();

        Assert.Contains("fetch-depth: 0", resolution.Message);
    }

    [Fact]
    public async Task Exit_4_under_Azure_DevOps_prints_the_line_to_add()
    {
        _runner.Returns(new(0, "true\n", ""), "--is-shallow-repository");
        _environment["TF_BUILD"] = "True";

        var resolution = await ResolveAsync();

        Assert.Contains("fetchDepth: 0", resolution.Message);

        // The GitHub spelling differs by one hyphen and would send an Azure user in circles.
        Assert.DoesNotContain("fetch-depth: 0", resolution.Message);
    }

    [Fact]
    public async Task Exit_4_off_a_known_provider_says_how_to_fix_it_anyway()
    {
        _runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/main");
        _runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/master");

        var resolution = await ResolveAsync();

        Assert.Contains("--base", resolution.Message);
    }

    [Fact]
    public async Task A_reference_that_has_no_merge_base_with_HEAD_is_exit_4()
    {
        _environment["GITHUB_BASE_REF"] = "main";
        _runner.Returns(new(1, "", ""), "merge-base");

        AssertUnresolvable(await ResolveAsync());
    }

    // ---- Notices ----------------------------------------------------------------------

    [Fact]
    public async Task Every_resolution_is_disclosed_with_the_sha_it_landed_on()
    {
        _environment["GITHUB_BASE_REF"] = "main";

        var resolution = await ResolveAsync();

        var notice = Assert.Single(resolution.Notices, n => n.Code == NoticeCodes.BaselineResolved);
        Assert.Equal(NoticeKind.Environment, notice.Kind);
        Assert.Contains(ForkPoint, notice.Message);
        Assert.Equal(ForkPoint, notice.Data?["sha"]);
        Assert.Equal("GITHUB_BASE_REF", notice.Data?["variable"]);
    }

    [Fact]
    public async Task A_baseline_resolving_to_HEAD_emits_its_own_notice()
    {
        _environment["GITHUB_BASE_REF"] = "main";
        _runner.Returns(new(0, $"{Head}\n", ""), "merge-base");

        var resolution = await ResolveAsync();

        // The empty-change-set trap *is* a baseline that quietly resolved to HEAD, and an
        // audit trail that cannot say what it audited will not catch it.
        Assert.True(resolution.Baseline!.IsHead);
        Assert.Contains(resolution.Notices, n => n.Code == NoticeCodes.BaselineIsHead);
    }

    [Fact]
    public async Task A_baseline_behind_HEAD_emits_no_HEAD_notice()
    {
        _environment["GITHUB_BASE_REF"] = "main";

        var resolution = await ResolveAsync();

        Assert.False(resolution.Baseline!.IsHead);
        Assert.DoesNotContain(resolution.Notices, n => n.Code == NoticeCodes.BaselineIsHead);
    }

    [Fact]
    public async Task A_failed_resolution_is_disclosed_too()
    {
        _runner.Returns(new(0, "true\n", ""), "--is-shallow-repository");

        var resolution = await ResolveAsync();

        Assert.Contains(resolution.Notices, n => n.Code == NoticeCodes.BaselineUnresolvable);
    }

    // ---- Helpers ----------------------------------------------------------------------

    private async Task AssertTrapAsync(string variable)
    {
        _environment[variable] = "the-branch-being-merged";
        _runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/main");
        _runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/master");

        var resolution = await ResolveAsync();

        Assert.False(
            resolution.Resolved,
            $"{variable} is a source branch and resolves at or ahead of HEAD, so reading it "
            + "produces an empty change set and a green pipeline.");

        Assert.DoesNotContain(
            _runner.Requests,
            request => request.Arguments.Contains("the-branch-being-merged"));
    }

    private static void AssertResolved(
        BaselineResolution resolution,
        BaselineOrigin origin,
        string reference)
    {
        Assert.True(resolution.Resolved, resolution.Message);
        Assert.Equal(ExitCode.Success, resolution.ExitCode);
        Assert.Equal(origin, resolution.Baseline!.Origin);
        Assert.Equal(reference, resolution.Baseline.Reference);
        Assert.Equal(ForkPoint, resolution.Baseline.Sha);
    }

    private static void AssertUnresolvable(BaselineResolution resolution)
    {
        Assert.False(resolution.Resolved);
        Assert.Null(resolution.Baseline);
        Assert.NotEmpty(resolution.Message);
    }
}
