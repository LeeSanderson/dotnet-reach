using Reach.Output;
using Reach.Processes;
using Reach.Tests.Cli;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Acceptance;

/// <summary>
/// Reach never deletes its files. The one claim no in-process test can prove: a test that calls
/// Reach in-process shares its lifetime, so only a child process that has exited shows what
/// Reach leaves behind for the next pipeline step.
/// </summary>
/// <remarks>
/// In this namespace because it starts a process, so it runs in the acceptance step.
/// </remarks>
public sealed class ReachDirectorySurvivalTests
{
    private const string Before = "namespace N; public class Widget { public int Spin() => 1; }";
    private const string After = "namespace N; public class Widget { public int Spin() => 42; }";

    private const string TestsSource =
        "namespace N.Tests; public class WidgetTests { [Xunit.Fact] public void Spins() => new N.Widget().Spin(); }";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_report_and_side_car_files_remain_after_the_process_exits()
    {
        using var repository = CommandLineTests.SolutionRepository();

        var widget = repository.WriteFile("src/Core/Widget.cs", Before);
        var tests = repository.WriteFile("src/Tests/WidgetTests.cs", TestsSource);

        // --runsettings is the cheapest way to make Reach write a side-car file: the merged
        // settings go into the directory Reach owns.
        repository.WriteFile("my.runsettings", "<RunSettings></RunSettings>\n");
        repository.Commit("widget");

        // A change since the baseline, with binaries built from it, so one test is selected.
        await File.WriteAllTextAsync(widget, After, Token);

        var core = Compiled.Assembly("Core", After, widget);
        core.WriteTo(repository.Combine("src", "Core", "bin", "Debug", "net10.0"));

        Compiled
            .Assembly("Tests", TestsSource, tests, references: [core])
            .WriteTo(repository.Combine("src", "Tests", "bin", "Debug", "net10.0"));

        var result = await new ProcessRunner().RunAsync(
            new ProcessRequest(
                "dotnet",
                [
                    Path.Combine(AppContext.BaseDirectory, "Reach.Cli.dll"),
                    "select", "--base", "main", "--no-build", "--runsettings", "my.runsettings",
                ],
                repository.Path),
            Token);

        Assert.True(result.ExitCode == 0, $"Reach exited {result.ExitCode}:\n{result.StandardOutput}{result.StandardError}");

        var directory = repository.Combine(ReachDirectory.DefaultName);

        Assert.True(File.Exists(Path.Combine(directory, "report.json")));
        Assert.True(File.Exists(Path.Combine(directory, "Tests.net10.0.0.runsettings")));
    }
}
