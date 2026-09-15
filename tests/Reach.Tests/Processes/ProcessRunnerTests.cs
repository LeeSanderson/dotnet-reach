using System.Diagnostics;
using Reach.Processes;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Processes;

/// <summary>
/// The only port in Reach, tested against real child processes. Everything it hides —
/// process lifetime, cancellation, stream draining, exit-code handling — fails in a way
/// no fake can reproduce, so these are the tests that earn a real process.
/// </summary>
public class ProcessRunnerTests
{
    private readonly ProcessRunner runner = new();

    [Fact]
    public async Task Reports_standard_output_and_a_zero_exit_code()
    {
        var result = await runner.RunAsync(
            new ProcessRequest("git", ["--version"], Environment.CurrentDirectory),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Succeeded);
        Assert.StartsWith("git version", result.StandardOutput);
    }

    [Fact]
    public async Task A_non_zero_exit_is_a_result_not_an_exception()
    {
        var result = await runner.RunAsync(
            new ProcessRequest(
                "git",
                ["rev-parse", "--verify", "no-such-ref-exists"],
                Environment.CurrentDirectory),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(0, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.StandardError);
    }

    [Fact]
    public async Task A_child_flooding_both_streams_does_not_deadlock()
    {
        const int Lines = 500;

        using var script = ChildScript.Chatty();

        var result = await runner.RunAsync(
            script.Invoke(Lines.ToString()),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(Lines, result.Lines.Count);
        Assert.Equal(Lines, result.StandardError.Split('\n').Count(line => line.Contains("err ")));

        // The point of the assertion: both streams carried far more than a pipe buffer, so
        // a runner that drained them one after the other would have hung rather than failed.
        Assert.True(result.StandardOutput.Length > 16 * 1024);
        Assert.True(result.StandardError.Length > 16 * 1024);
    }

    [Fact]
    public async Task Cancellation_kills_the_child_and_everything_it_started()
    {
        using var script = ChildScript.Spawner();
        using var beats = TempDirectory.Create("reach-beats");

        var childBeat = beats.Combine("child.txt");
        var grandchildBeat = beats.Combine("grandchild.txt");

        using var cancellation = new CancellationTokenSource();
        var run = runner.RunAsync(script.Invoke(childBeat, grandchildBeat), cancellation.Token);

        await WaitUntil(() => Beats(childBeat) >= 2 && Beats(grandchildBeat) >= 2);

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        var (childAfterKill, grandchildAfterKill) = (Beats(childBeat), Beats(grandchildBeat));
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);

        Assert.Equal(childAfterKill, Beats(childBeat));

        // The direct child is rarely the whole story: `dotnet build` leaves MSBuild nodes
        // behind, and they hold the output directory open.
        Assert.Equal(grandchildAfterKill, Beats(grandchildBeat));
    }

    [Fact]
    public async Task Arguments_and_working_directories_survive_spaces_and_non_ascii()
    {
        using var repository = TempRepository.Create("reach repo café-Ω");

        const string Spaced = "a file with spaces.cs";
        const string NonAscii = "café-Ω.cs";

        repository.WriteFile(Spaced, "class A;");
        repository.WriteFile(NonAscii, "class B;");
        repository.Commit("baseline");

        // -c core.quotePath=false is load-bearing, not tidiness: without it git returns
        // "caf\303\251-\316\251.cs", which matches no file in the working tree.
        var result = await runner.RunAsync(
            new ProcessRequest(
                "git",
                ["-c", "core.quotePath=false", "ls-files", "--", Spaced, NonAscii],
                repository.Path),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal([Spaced, NonAscii], result.Lines.Order(StringComparer.Ordinal));
    }

    private static int Beats(string path)
    {
        try
        {
            return File.ReadAllLines(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = Stopwatch.StartNew();

        while (!condition())
        {
            Assert.True(deadline.Elapsed < TimeSpan.FromSeconds(30), "the child never got going");
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
