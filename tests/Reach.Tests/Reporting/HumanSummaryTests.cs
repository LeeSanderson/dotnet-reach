using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Reporting;

/// <summary>
/// The summary is explicitly not a contract, so nothing here asserts its wording for its own
/// sake. What is asserted is the three things a normal run has to get right, because a normal
/// run <em>is</em> the preview — there is no dry-run verb.
/// </summary>
public class HumanSummaryTests
{
    private const string Core = """
        namespace N;

        public class Widget
        {
            public int Spin() => Inner();
            public int Inner() => 1;
        }
        """;

    private const string Reaching = """
        namespace N.Tests;

        public class WidgetTests
        {
            [Xunit.Fact]
            public void Spins() => new N.Widget().Spin();
        }
        """;

    private const string NotReaching = """
        namespace N.Tests;

        public class WidgetTests
        {
            [Xunit.Fact]
            public void Unrelated() { }
        }
        """;

    private static string SummaryOf(Report report) => HumanSummary.Write(report, "/work/.reach/report.json", "/work/.reach");

    [Fact]
    public void The_resolved_baseline_sha_and_its_detection_source_lead_every_run()
    {
        var report = ReportFixture.Minimal() with
        {
            Envelope = ReportFixture.Minimal().Envelope with
            {
                Baseline = new ReportBaseline(
                    "0123456789abcdef0123456789abcdef01234567",
                    "origin/main",
                    "GitHubActions",
                    IsHead: false),
            },
        };

        var summary = SummaryOf(report);

        // An audit line nobody reads does not catch a baseline that resolved to HEAD, so it
        // leads rather than trails.
        Assert.StartsWith("Baseline 0123456789", summary);
        Assert.Contains("origin/main", summary);
        Assert.Contains("GitHubActions", summary);
    }

    [Fact]
    public void A_baseline_that_resolved_to_HEAD_says_so_on_the_first_line()
    {
        var report = ReportFixture.Minimal() with
        {
            Envelope = ReportFixture.Minimal().Envelope with
            {
                Baseline = new ReportBaseline("abc", "main", "Option", IsHead: true),
            },
        };

        // The empty-change-set trap *is* a baseline that quietly resolved to HEAD.
        Assert.Contains("which is HEAD", SummaryOf(report).Split('\n')[0]);
    }

    [Fact]
    public void No_changes_and_nothing_selected_get_visibly_different_verdicts()
    {
        using var selections = Selections.Of(Core, NotReaching, changed: ["Widget.Inner"]);

        var nothingSelected = SummaryOf(ReportFixture.Build(selections));
        var noChanges = SummaryOf(ReportFixture.Build(selections, noChanges: true));

        // They exit the same and mean opposite things — a docs-only pull request, against code
        // changes that reached nothing.
        Assert.Contains("No changes to analyse", noChanges);
        Assert.DoesNotContain("No changes to analyse", nothingSelected);

        Assert.Contains("There WERE changes", nothingSelected);
        Assert.DoesNotContain("There WERE changes", noChanges);
    }

    [Fact]
    public void The_reason_codes_lead_the_nothing_selected_output()
    {
        using var selections = Selections.Of(Core, NotReaching, changed: ["Widget.Inner"]);

        var report = ReportFixture.Build(selections);
        var summary = SummaryOf(report);

        Assert.Equal("nothing-selected", report.Outcome);

        // This is the result most likely to be disbelieved, so the codes come before anything
        // else the verdict has to say.
        var verdict = summary.Split('\n').First(line => line.Contains("Nothing selected", StringComparison.Ordinal));

        Assert.Contains(Assert.Single(report.Reasons!), verdict);
    }

    [Fact]
    public void Every_blind_spot_notice_is_printed_in_full()
    {
        var report = ReportFixture.Minimal() with
        {
            Notices =
            [
                new ReportNotice
                {
                    Code = "ignored-untracked-assembly",
                    Kind = "blind-spot",
                    Message = "One compiled source file is untracked and git-ignored.",
                },
                new ReportNotice
                {
                    Code = "signature-ambiguous",
                    Kind = "widening",
                    Message = "Two candidates matched, so both got an edge.",
                },
            ],
        };

        var summary = SummaryOf(report);

        // A blind spot says Reach may have missed something, and a count alone would bury it.
        Assert.Contains("One compiled source file is untracked", summary);
        Assert.DoesNotContain("Two candidates matched", summary);

        // The widening one is still counted.
        Assert.Contains("1 widening notice(s)", summary);
    }

    [Fact]
    public void The_summary_says_where_the_report_and_the_side_car_files_went()
    {
        var summary = SummaryOf(ReportFixture.Minimal());

        Assert.Contains("/work/.reach/report.json", summary);
        Assert.Contains("/work/.reach", summary);
    }

    [Fact]
    public void The_summary_never_lists_the_selected_tests()
    {
        using var selections = Selections.Of(Core, Reaching, changed: ["Widget.Inner"]);

        var report = ReportFixture.Build(selections);
        var summary = SummaryOf(report);

        Assert.Equal("selected", report.Outcome);

        // Over-selection stated as a percentage of the suite, because that is the number that
        // decides adoption.
        Assert.Contains("selected 1 of 1 tests (100%)", summary);

        // Counts, not names. The list lives in the report.
        Assert.DoesNotContain("Spins", summary);
    }

    [Fact]
    public void A_failed_run_says_to_run_the_whole_suite()
    {
        var report = ReportFixture.Minimal() with { Outcome = "failed" };

        // The one line of pipeline guidance the exit codes exist to make operational.
        Assert.Contains("Run the whole suite, or stop the build", SummaryOf(report));
    }
}
