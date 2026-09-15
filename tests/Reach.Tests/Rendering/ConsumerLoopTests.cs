using Reach.Processes;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Rendering;

/// <summary>
/// The consumer's half of the empty-selection contract, driven rather than inferred.
/// </summary>
/// <remarks>
/// Two opposite failure modes are closed by the same fact — an empty filter runs everything,
/// and under MTP an empty-match filter exits 8 and turns a green build red — so "emit no
/// command at all" has to be observable from the consumer's side, not just true in the writer.
/// </remarks>
public class ConsumerLoopTests
{
    private const string Core = """
        namespace N;

        public class Widget
        {
            public int Spin() => Inner();
            public int Inner() => 1;
        }
        """;

    /// <summary>What a pipeline step does with the report: loop, and run each invocation.</summary>
    private static async Task<int> DriveAsync(
        Reach.Reporting.Report report,
        IProcessRunner runner,
        CancellationToken cancellationToken)
    {
        var issued = 0;

        foreach (var entry in report.Entries)
        {
            foreach (var invocation in entry.Invocations)
            {
                issued++;

                await runner.RunAsync(
                    new ProcessRequest(invocation[0], [.. invocation.Skip(1)], Environment.CurrentDirectory),
                    cancellationToken);
            }
        }

        return issued;
    }

    [Fact]
    public async Task A_loop_over_an_empty_selection_starts_no_process_at_all()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Unrelated() { }
            }
            """,
            changed: ["Widget.Inner"]);

        var report = ReportFixture.Build(selections);
        var runner = new ScriptedProcessRunner();

        Assert.Equal("nothing-selected", report.Outcome);

        var issued = await DriveAsync(report, runner, TestContext.Current.CancellationToken);

        // Not one command. An unscripted command would have thrown, so even a wrong one would
        // have been loud.
        Assert.Equal(0, issued);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task A_loop_over_a_real_selection_starts_exactly_one_process()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Spins() => new N.Widget().Spin();
            }
            """,
            changed: ["Widget.Inner"]);

        var report = ReportFixture.Build(selections);
        var runner = new ScriptedProcessRunner();

        runner.Succeeds(string.Empty, "test");

        Assert.Equal("selected", report.Outcome);
        Assert.Equal(1, await DriveAsync(report, runner, TestContext.Current.CancellationToken));

        var request = Assert.Single(runner.Requests);

        Assert.Equal("dotnet", request.Executable);
        Assert.Contains("--filter", request.Arguments);
    }

    [Fact]
    public async Task A_loop_over_a_no_changes_report_starts_no_process_either()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Spins() => new N.Widget().Spin();
            }
            """,
            changed: ["Widget.Inner"]);

        var report = ReportFixture.Build(selections, noChanges: true);
        var runner = new ScriptedProcessRunner();

        // A consumer's loop behaves identically across all four outcomes; omitting entries
        // would make it a special case, and the consumer that forgets runs the full suite on
        // an empty diff.
        Assert.Equal("no-changes", report.Outcome);
        Assert.NotEmpty(report.Entries);
        Assert.Equal(0, await DriveAsync(report, runner, TestContext.Current.CancellationToken));
    }
}
