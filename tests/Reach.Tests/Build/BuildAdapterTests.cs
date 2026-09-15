using Reach.Build;
using Reach.Projects;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Build;

/// <summary>
/// The second adapter at the one port. Driven entirely through the fake, because what matters
/// is the argument vector and the exit code, and neither needs a real build to establish.
/// </summary>
public class BuildAdapterTests
{
    private readonly ScriptedProcessRunner runner = new();

    private static DiscoveredTarget Target { get; } =
        new(Path.Combine("C:", "work", "Solution.slnx"), TargetKind.Solution);

    private static SelectRequest Request { get; } =
        new() { WorkingDirectory = Path.Combine("C:", "work") };

    private Task<BuildOutcome> Build(SelectRequest request) =>
        new BuildAdapter(runner).BuildAsync(Target, request, TestContext.Current.CancellationToken);

    [Fact]
    public async Task The_default_mode_invokes_dotnet_build_on_the_target()
    {
        runner.Succeeds("Build succeeded.", "build");

        var outcome = await Build(Request);

        Assert.True(outcome.Succeeded);
        Assert.Equal("dotnet", Assert.Single(runner.Requests).Executable);
        Assert.Equal(["build", Target.Path], runner.Requests[0].Arguments);
    }

    [Fact]
    public async Task No_build_never_invokes_the_build_adapter()
    {
        var outcome = await Build(Request with { NoBuild = true });

        Assert.True(outcome.Succeeded);
        Assert.Null(outcome.Request);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task The_layout_options_reach_the_build()
    {
        runner.Succeeds("", "build");

        await Build(Request with
        {
            Configuration = "Release",
            Output = "out",
            ArtifactsPath = "artifacts",
        });

        // Passing them is what makes Reach's answer and the build's output agree, and is why
        // a forwarded copy of one after `--` is refused.
        Assert.Equal(
            ["build", Target.Path, "--configuration", "Release", "--output", "out", "--artifacts-path", "artifacts"],
            runner.Requests[0].Arguments);
    }

    [Fact]
    public async Task Forwarded_tokens_reach_the_build_verbatim_and_last()
    {
        runner.Succeeds("", "build");

        await Build(Request with
        {
            Configuration = "Release",
            ForwardedBuildArguments = ["-p:ContinuousIntegrationBuild=true", "--no-restore"],
        });

        Assert.Equal(
            ["build", Target.Path, "--configuration", "Release", "-p:ContinuousIntegrationBuild=true", "--no-restore"],
            runner.Requests[0].Arguments);
    }

    [Fact]
    public async Task A_failed_build_is_a_result_carrying_what_the_build_said()
    {
        runner.Fails(1, "error CS1002: ; expected", "build");

        var outcome = await Build(Request);

        // There is no degraded mode: a failed build already fails the pipeline, so a clever
        // selection over a broken tree has no consumer.
        Assert.False(outcome.Succeeded);
        Assert.Contains("CS1002", outcome.Output);
        Assert.NotNull(outcome.Request);
    }

    [Fact]
    public async Task The_command_that_ran_is_recorded()
    {
        runner.Succeeds("", "build");

        var outcome = await Build(Request);

        // So a surprising run stays reproducible from the report envelope alone.
        Assert.Equal(runner.Requests[0], outcome.Request);
    }
}
