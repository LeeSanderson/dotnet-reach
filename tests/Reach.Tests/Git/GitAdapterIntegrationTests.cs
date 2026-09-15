using Reach.Git;
using Reach.Processes;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Git;

/// <summary>
/// The adapter against real git in a real repository. The scripted tests pin the argv;
/// these pin that the argv means what it is supposed to mean, which no fake can tell us.
/// </summary>
public class GitAdapterIntegrationTests
{
    private static GitAdapter AdapterFor(TempRepository repository) =>
        new(new ProcessRunner(), repository.Path);

    [Fact]
    public async Task A_merge_base_is_where_the_branch_left_the_trunk()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile("src/A.cs", "class A;");
        var forkPoint = repository.Commit("baseline");

        repository.CheckoutNewBranch("feature");
        repository.WriteFile("src/B.cs", "class B;");
        repository.Commit("on the branch");

        // The trunk moves on after the fork, which is the whole reason the baseline is the
        // merge-base and not the target branch's tip.
        repository.Checkout("main");
        repository.WriteFile("src/C.cs", "class C;");
        repository.Commit("on the trunk");
        repository.Checkout("feature");

        var result = await AdapterFor(repository)
            .MergeBaseAsync("main", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(forkPoint, result.Value);
    }

    [Fact]
    public async Task A_missing_merge_base_is_a_non_zero_exit_not_an_exception()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile("src/A.cs", "class A;");
        repository.Commit("baseline");

        // An orphan branch shares no history with main, so there is no merge-base to find —
        // which is what a shallow CI clone looks like from here.
        repository.Git("checkout", "--orphan", "unrelated");
        repository.WriteFile("src/D.cs", "class D;");
        repository.Commit("unrelated history");

        var result = await AdapterFor(repository)
            .MergeBaseAsync("main", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task The_changed_set_spans_committed_staged_and_unstaged_in_one_shot()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile("src/Committed.cs", "class Committed;");
        repository.WriteFile("src/Staged.cs", "class Staged;");
        repository.WriteFile("src/Unstaged.cs", "class Unstaged;");
        var baseline = repository.Commit("baseline");

        repository.WriteFile("src/Committed.cs", "class Committed { }");
        repository.Commit("committed since the baseline");

        repository.WriteFile("src/Staged.cs", "class Staged { }");
        repository.Git("add", "src/Staged.cs");

        repository.WriteFile("src/Unstaged.cs", "class Unstaged { }");

        var result = await AdapterFor(repository)
            .ChangedPathsAsync(baseline, TestContext.Current.CancellationToken);

        Assert.Equal(
            ["M\tsrc/Committed.cs", "M\tsrc/Staged.cs", "M\tsrc/Unstaged.cs"],
            result.Lines.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_moved_file_is_a_delete_and_an_add_rather_than_a_rename()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile("src/Before.cs", "class Moved { void M() { } }");
        var baseline = repository.Commit("baseline");

        repository.Git("mv", "src/Before.cs", "src/After.cs");
        repository.Commit("moved");

        var result = await AdapterFor(repository)
            .ChangedPathsAsync(baseline, TestContext.Current.CancellationToken);

        // --no-renames is why: with renames on, git reports one R line at whatever its
        // similarity threshold decides, and the threshold's wrong setting under-selects.
        Assert.Equal(
            ["A\tsrc/After.cs", "D\tsrc/Before.cs"],
            result.Lines.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Untracked_files_come_back_but_ignored_ones_do_not()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile(".gitignore", "obj/\n");
        repository.Commit("baseline");

        repository.WriteFile("src/New.cs", "class New;");
        repository.WriteFile("obj/Generated.cs", "class Generated;");

        var result = await AdapterFor(repository)
            .UntrackedPathsAsync(TestContext.Current.CancellationToken);

        // Without --exclude-standard every generated .cs under obj/ returns as untracked
        // and every project's assembly widens on every run.
        Assert.Equal(["src/New.cs"], result.Lines);
    }

    [Fact]
    public async Task A_file_reads_back_at_the_baseline_and_a_file_that_was_not_there_does_not()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile("src/A.cs", "class A;");
        var baseline = repository.Commit("baseline");

        repository.WriteFile("src/A.cs", "class A { }");
        repository.WriteFile("src/Added.cs", "class Added;");
        repository.Commit("changed");

        var adapter = AdapterFor(repository);

        var atBaseline = await adapter.FileAtBaselineAsync(
            baseline, "src/A.cs", TestContext.Current.CancellationToken);
        Assert.Equal("class A;", atBaseline.Value);

        var neverThere = await adapter.FileAtBaselineAsync(
            baseline, "src/Added.cs", TestContext.Current.CancellationToken);
        Assert.False(neverThere.Succeeded);
    }

    [Fact]
    public async Task A_full_clone_is_not_shallow_and_reports_its_own_root()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile("src/A.cs", "class A;");
        repository.Commit("baseline");

        var adapter = AdapterFor(repository);

        var shallow = await adapter.IsShallowCloneAsync(TestContext.Current.CancellationToken);
        Assert.Equal("false", shallow.Value);

        var root = await adapter.RepositoryRootAsync(TestContext.Current.CancellationToken);
        Assert.True(root.Succeeded);
        Assert.Equal(
            Path.GetFileName(repository.Path),
            Path.GetFileName(root.Value.TrimEnd('/')));
    }

    [Fact]
    public async Task A_shallow_clone_says_so()
    {
        using var origin = TempRepository.Create("reach-origin");

        origin.WriteFile("src/A.cs", "class A;");
        origin.Commit("first");
        origin.WriteFile("src/B.cs", "class B;");
        origin.Commit("second");

        using var clone = TempDirectory.Create("reach-shallow");
        var clonePath = Path.Combine(clone.Path, "work");

        var cloned = await new ProcessRunner().RunAsync(
            new ProcessRequest(
                "git",
                ["clone", "--depth", "1", "file://" + origin.Path.Replace('\\', '/'), clonePath],
                clone.Path),
            TestContext.Current.CancellationToken);

        Assert.True(cloned.Succeeded, cloned.StandardError);

        var result = await new GitAdapter(new ProcessRunner(), clonePath)
            .IsShallowCloneAsync(TestContext.Current.CancellationToken);

        Assert.Equal("true", result.Value);
    }
}
