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

    private readonly ScriptedProcessRunner runner = new();
    private readonly Dictionary<string, string?> environment = new(StringComparer.Ordinal);

    public BaselineResolverTests()
    {
        runner.Succeeds("false\n", "--is-shallow-repository");
        runner.Succeeds($"{ForkPoint}\n", "merge-base");
        runner.Succeeds($"{Head}\n", "rev-parse", "HEAD");
    }

    private Task<BaselineResolution> Resolve(string? baseReference = null) =>
        new BaselineResolver(new GitAdapter(runner, "/work"))
            .ResolveAsync(baseReference, Lookup, TestContext.Current.CancellationToken);

    private string? Lookup(string name) =>
        environment.TryGetValue(name, out var value) ? value : null;

    // ---- The ladder -------------------------------------------------------------------

    [Fact]
    public async Task The_option_wins_over_every_variable()
    {
        environment["GITHUB_BASE_REF"] = "main";

        var resolution = await Resolve("release/1.0");

        AssertResolved(resolution, BaselineOrigin.Option, "release/1.0");
    }

    [Fact]
    public async Task Rung_1_is_GITHUB_BASE_REF()
    {
        environment["GITHUB_BASE_REF"] = "main";

        AssertResolved(await Resolve(), BaselineOrigin.GitHubActions, "main");
    }

    [Fact]
    public async Task Rung_2_is_the_Azure_DevOps_target_branch()
    {
        environment["SYSTEM_PULLREQUEST_TARGETBRANCH"] = "refs/heads/develop";

        AssertResolved(await Resolve(), BaselineOrigin.AzureDevOps, "develop");
    }

    [Fact]
    public async Task Rung_3_is_the_GitLab_merge_request_target()
    {
        environment["CI_MERGE_REQUEST_TARGET_BRANCH_NAME"] = "main";

        AssertResolved(await Resolve(), BaselineOrigin.GitLabCi, "main");
    }

    [Fact]
    public async Task Rung_4_is_the_Bitbucket_destination_branch()
    {
        environment["BITBUCKET_PR_DESTINATION_BRANCH"] = "main";

        AssertResolved(await Resolve(), BaselineOrigin.BitbucketPipelines, "main");
    }

    [Fact]
    public async Task Rung_5_is_the_Jenkins_change_target()
    {
        environment["CHANGE_TARGET"] = "main";

        AssertResolved(await Resolve(), BaselineOrigin.Jenkins, "main");
    }

    [Fact]
    public async Task Rung_6_probes_for_a_local_remote_branch_and_needs_exactly_one()
    {
        runner.Succeeds("abc\n", "rev-parse", "origin/main");
        runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/master");

        AssertResolved(await Resolve(), BaselineOrigin.LocalRemoteBranch, "origin/main");
    }

    [Fact]
    public async Task Rung_6_declines_when_both_local_remote_branches_exist()
    {
        runner.Succeeds("abc\n", "rev-parse", "origin/main");
        runner.Succeeds("def\n", "rev-parse", "origin/master");

        AssertUnresolvable(await Resolve());
    }

    [Fact]
    public async Task Rung_7_is_exit_4()
    {
        runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/main");
        runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/master");

        var resolution = await Resolve();

        AssertUnresolvable(resolution);
        Assert.Equal(ExitCode.BaselineUnresolvable, resolution.ExitCode);
    }

    [Fact]
    public async Task The_first_hit_wins()
    {
        environment["GITHUB_BASE_REF"] = "from-github";
        environment["CI_MERGE_REQUEST_TARGET_BRANCH_NAME"] = "from-gitlab";
        environment["CHANGE_TARGET"] = "from-jenkins";

        AssertResolved(await Resolve(), BaselineOrigin.GitHubActions, "from-github");
    }

    [Fact]
    public async Task An_empty_variable_is_not_a_hit()
    {
        environment["GITHUB_BASE_REF"] = "";
        environment["CHANGE_TARGET"] = "main";

        AssertResolved(await Resolve(), BaselineOrigin.Jenkins, "main");
    }

    [Theory]
    [InlineData("refs/heads/main", "main")]
    [InlineData("main", "main")]
    [InlineData("refs/heads/feature/a-b", "feature/a-b")]
    public async Task Azures_two_formats_strip_to_the_same_branch_name(string variable, string expected)
    {
        // The format varies by repo provider: refs/heads/main for Azure Repos, bare main for
        // a GitHub-hosted repository behind an Azure pipeline.
        environment["SYSTEM_PULLREQUEST_TARGETBRANCH"] = variable;

        AssertResolved(await Resolve(), BaselineOrigin.AzureDevOps, expected);
    }

    // ---- The source-branch traps ------------------------------------------------------
    //
    // Picking a source branch is the one detection bug that under-selects rather than
    // over-selects: it resolves at or ahead of HEAD, giving an empty change set and a green
    // pipeline. Each gets its own named test so the failure names the variable.

    [Fact]
    public async Task GITHUB_HEAD_REF_is_never_read() => await AssertTrap("GITHUB_HEAD_REF");

    [Fact]
    public async Task SYSTEM_PULLREQUEST_SOURCEBRANCH_is_never_read() =>
        await AssertTrap("SYSTEM_PULLREQUEST_SOURCEBRANCH");

    [Fact]
    public async Task BUILD_SOURCEBRANCH_is_never_read() => await AssertTrap("BUILD_SOURCEBRANCH");

    [Fact]
    public async Task CI_MERGE_REQUEST_SOURCE_BRANCH_NAME_is_never_read() =>
        await AssertTrap("CI_MERGE_REQUEST_SOURCE_BRANCH_NAME");

    [Fact]
    public async Task BITBUCKET_BRANCH_is_never_read() => await AssertTrap("BITBUCKET_BRANCH");

    [Fact]
    public async Task CHANGE_BRANCH_is_never_read() => await AssertTrap("CHANGE_BRANCH");

    // ---- Shallow clones and the exit-4 message ----------------------------------------

    [Fact]
    public async Task A_shallow_clone_is_exit_4_before_anything_else_is_tried()
    {
        runner.Returns(new(0, "true\n", ""), "--is-shallow-repository");
        environment["GITHUB_BASE_REF"] = "main";

        var resolution = await Resolve();

        AssertUnresolvable(resolution);
        Assert.Contains("shallow", resolution.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exit_4_under_GitHub_Actions_prints_the_line_to_add()
    {
        runner.Returns(new(0, "true\n", ""), "--is-shallow-repository");
        environment["GITHUB_ACTIONS"] = "true";

        var resolution = await Resolve();

        Assert.Contains("fetch-depth: 0", resolution.Message);
    }

    [Fact]
    public async Task Exit_4_under_Azure_DevOps_prints_the_line_to_add()
    {
        runner.Returns(new(0, "true\n", ""), "--is-shallow-repository");
        environment["TF_BUILD"] = "True";

        var resolution = await Resolve();

        Assert.Contains("fetchDepth: 0", resolution.Message);

        // The GitHub spelling differs by one hyphen and would send an Azure user in circles.
        Assert.DoesNotContain("fetch-depth: 0", resolution.Message);
    }

    [Fact]
    public async Task Exit_4_off_a_known_provider_says_how_to_fix_it_anyway()
    {
        runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/main");
        runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/master");

        var resolution = await Resolve();

        Assert.Contains("--base", resolution.Message);
    }

    [Fact]
    public async Task A_reference_that_has_no_merge_base_with_HEAD_is_exit_4()
    {
        environment["GITHUB_BASE_REF"] = "main";
        runner.Returns(new(1, "", ""), "merge-base");

        AssertUnresolvable(await Resolve());
    }

    // ---- Notices ----------------------------------------------------------------------

    [Fact]
    public async Task Every_resolution_is_disclosed_with_the_sha_it_landed_on()
    {
        environment["GITHUB_BASE_REF"] = "main";

        var resolution = await Resolve();

        var notice = Assert.Single(resolution.Notices, n => n.Code == NoticeCodes.BaselineResolved);
        Assert.Equal(NoticeKind.Environment, notice.Kind);
        Assert.Contains(ForkPoint, notice.Message);
        Assert.Equal(ForkPoint, notice.Data?["sha"]);
        Assert.Equal("GITHUB_BASE_REF", notice.Data?["variable"]);
    }

    [Fact]
    public async Task A_baseline_resolving_to_HEAD_emits_its_own_notice()
    {
        environment["GITHUB_BASE_REF"] = "main";
        runner.Returns(new(0, $"{Head}\n", ""), "merge-base");

        var resolution = await Resolve();

        // The empty-change-set trap *is* a baseline that quietly resolved to HEAD, and an
        // audit trail that cannot say what it audited will not catch it.
        Assert.True(resolution.Baseline!.IsHead);
        Assert.Contains(resolution.Notices, n => n.Code == NoticeCodes.BaselineIsHead);
    }

    [Fact]
    public async Task A_baseline_behind_HEAD_emits_no_HEAD_notice()
    {
        environment["GITHUB_BASE_REF"] = "main";

        var resolution = await Resolve();

        Assert.False(resolution.Baseline!.IsHead);
        Assert.DoesNotContain(resolution.Notices, n => n.Code == NoticeCodes.BaselineIsHead);
    }

    [Fact]
    public async Task A_failed_resolution_is_disclosed_too()
    {
        runner.Returns(new(0, "true\n", ""), "--is-shallow-repository");

        var resolution = await Resolve();

        Assert.Contains(resolution.Notices, n => n.Code == NoticeCodes.BaselineUnresolvable);
    }

    // ---- Helpers ----------------------------------------------------------------------

    private async Task AssertTrap(string variable)
    {
        environment[variable] = "the-branch-being-merged";
        runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/main");
        runner.Fails(128, "fatal: unknown revision\n", "rev-parse", "origin/master");

        var resolution = await Resolve();

        Assert.False(
            resolution.Resolved,
            $"{variable} is a source branch and resolves at or ahead of HEAD, so reading it "
            + "produces an empty change set and a green pipeline.");

        Assert.DoesNotContain(
            runner.Requests,
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
