using Reach.Baselines;
using Reach.Git;
using Reach.Processes;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Baselines;

/// <summary>
/// The two facts about resolution that only a real repository can establish: what a shallow
/// clone does, and what merge-base returns from the detached merge commit a CI provider
/// actually checks out.
/// </summary>
public class BaselineResolverIntegrationTests
{
    private static string? NoEnvironment(string name) => null;

    [Fact]
    public async Task A_shallow_clone_is_exit_4()
    {
        using var origin = TempRepository.Create("reach-origin");

        origin.WriteFile("src/A.cs", "class A;");
        origin.Commit("first");
        origin.WriteFile("src/B.cs", "class B;");
        origin.Commit("second");

        using var workspace = TempDirectory.Create("reach-shallow");
        var clonePath = Path.Combine(workspace.Path, "work");

        var cloned = await new ProcessRunner().RunAsync(
            new ProcessRequest(
                "git",
                ["clone", "--depth", "1", "file://" + origin.Path.Replace('\\', '/'), clonePath],
                workspace.Path),
            TestContext.Current.CancellationToken);

        Assert.True(cloned.Succeeded, cloned.StandardError);

        var resolution = await new BaselineResolver(new GitAdapter(new ProcessRunner(), clonePath))
            .ResolveAsync("origin/main", NoEnvironment, TestContext.Current.CancellationToken);

        Assert.False(resolution.Resolved);
        Assert.Equal(ExitCode.BaselineUnresolvable, resolution.ExitCode);

        // Never a silently truncated history: the message has to say which problem this is.
        Assert.Contains("shallow", resolution.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_merge_commit_checkout_resolves_to_exactly_the_target_commit()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile("src/A.cs", "class A;");
        repository.Commit("A on main");

        repository.CheckoutNewBranch("feature");
        repository.WriteFile("src/B.cs", "class B;");
        repository.Commit("B on the branch");

        repository.Checkout("main");
        repository.WriteFile("src/C.cs", "class C;");
        var targetCommit = repository.Commit("C on main");

        // What a CI provider checks out on a pull-request build: a detached merge of the
        // branch and the target's tip, which is not a commit on either branch.
        repository.Git("checkout", "--detach", "feature");
        repository.Git("merge", "main", "--no-edit");
        var mergeCommit = repository.Git("rev-parse", "HEAD").Trim();

        // And the target branch keeps moving while the build runs.
        repository.Git("checkout", "main");
        repository.WriteFile("src/D.cs", "class D;");
        repository.Commit("D on main");
        repository.Git("checkout", mergeCommit);

        var resolution = await new BaselineResolver(new GitAdapter(new ProcessRunner(), repository.Path))
            .ResolveAsync("main", NoEnvironment, TestContext.Current.CancellationToken);

        Assert.True(resolution.Resolved, resolution.Message);
        Assert.Equal(targetCommit, resolution.Baseline!.Sha);
        Assert.False(resolution.Baseline.IsHead);
    }

    [Fact]
    public async Task A_baseline_that_lands_on_HEAD_is_detected_against_a_real_repository()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile("src/A.cs", "class A;");
        repository.Commit("baseline");

        repository.CheckoutNewBranch("feature");

        // The branch has no commits of its own, so merge-base(HEAD, main) is HEAD.
        var resolution = await new BaselineResolver(new GitAdapter(new ProcessRunner(), repository.Path))
            .ResolveAsync("main", NoEnvironment, TestContext.Current.CancellationToken);

        Assert.True(resolution.Resolved, resolution.Message);
        Assert.True(resolution.Baseline!.IsHead);
        Assert.Contains(
            resolution.Notices,
            notice => notice.Code == Reach.Reporting.NoticeCodes.BaselineIsHead);
    }
}
