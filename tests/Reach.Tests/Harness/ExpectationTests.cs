using Reach.Reporting;
using Reach.Tests.Acceptance;
using Reach.Tests.Fixtures;
using Outcome = Reach.Tests.Acceptance.Outcome;

namespace Reach.Tests.Harness;

/// <summary>
/// What <c>Expect</c> compares and which invariants it holds, against hand-built reports. A
/// comparison that never fails proves nothing, so every section and every invariant has a test
/// that makes it fail and reads the message.
/// </summary>
public sealed class ExpectationTests
{
    private const string TempRoot = "/tmp/reach-basics-0";

    private const string Because = "Only Discount.Apply changed, and two tests reach it.";

    private static readonly Expected Matching = new()
    {
        Because = Because,
        Outcome = Outcome.Selected,
        Selected = new()
        {
            ["Basics.Tests"] = ["Basics.Tests.DiscountTests.Ten_percent_off_a_hundred_is_ninety"],
        },
        RunInFull = ["Basics.Unrecognised"],
    };

    [Fact]
    public void A_report_that_matches_every_given_section_passes() =>
        Assert.Null(Failures(Matching, SelectedReport()));

    [Fact]
    public void The_failure_puts_because_first_and_lists_every_differing_section_at_once()
    {
        var message = Failures(
            Matching with { Outcome = Outcome.NothingSelected, RunInFull = [] },
            SelectedReport());

        Assert.NotNull(message);
        Assert.StartsWith("Because: " + Because, message, StringComparison.Ordinal);
        Assert.Contains("Outcome:", message, StringComparison.Ordinal);
        Assert.Contains("RunInFull:", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failure_never_prints_a_pasteable_expected_literal()
    {
        var message = Failures(Matching with { Selected = new() }, SelectedReport())!;

        Assert.DoesNotContain("new()", message, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Selected =", message, StringComparison.Ordinal);
    }

    // ---- The required sections ---------------------------------------------------------------

    [Fact]
    public void A_different_outcome_fails_naming_both()
    {
        var message = Failures(Matching with { Outcome = Outcome.NoChanges }, SelectedReport());

        Assert.Contains("Outcome:\n  expected no-changes, was selected", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_test_missing_from_the_selection_fails_naming_it()
    {
        var expected = Matching with
        {
            Selected = new()
            {
                ["Basics.Tests"] =
                [
                    "Basics.Tests.DiscountTests.Ten_percent_off_a_hundred_is_ninety",
                    "Basics.Tests.BasketTests.A_discounted_basket_totals_less_than_its_prices",
                ],
            },
        };

        var message = Failures(expected, SelectedReport());

        Assert.Contains(
            "Selected:\n  Basics.Tests: not selected Basics.Tests.BasketTests.A_discounted_basket_totals_less_than_its_prices",
            message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_selected_test_nobody_expected_fails_naming_it_even_in_a_project_not_listed()
    {
        var report = SelectedReport(
            Entry("tests/Other.Tests/Other.Tests.csproj", "net10.0", "filtered", 4, "Other.Tests.ATests.Runs"));

        var message = Failures(Matching, report);

        Assert.Contains("Selected:\n  Other.Tests: selected but not expected Other.Tests.ATests.Runs", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_run_in_full_that_was_not_expected_fails_and_so_does_one_that_was_but_is_not()
    {
        var message = Failures(Matching with { RunInFull = ["Basics.Tests"] }, SelectedReport());

        Assert.Contains("RunInFull:", message, StringComparison.Ordinal);
        Assert.Contains("Basics.Tests: expected to run in full, mode was filtered", message, StringComparison.Ordinal);
        Assert.Contains("Basics.Unrecognised: runs in full, not expected", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_outcome_without_an_exit_code_fails()
    {
        var message = Failures(Matching with { Outcome = Outcome.Failed }, FailedReport(), exitCode: 2);

        Assert.Contains("ExitCode:\n  required when Outcome is Failed", message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_exit_code_on_any_other_outcome_fails()
    {
        var message = Failures(Matching with { ExitCode = 0 }, SelectedReport());

        Assert.Contains("ExitCode:\n  given for outcome selected; only Failed takes one", message, StringComparison.Ordinal);
    }

    // ---- The optional sections ---------------------------------------------------------------

    [Fact]
    public void A_different_will_run_count_fails_and_an_unlisted_project_that_runs_tests_does_too()
    {
        var message = Failures(Matching with { WillRun = new() { ["Basics.Tests"] = 3 } }, SelectedReport());

        Assert.Contains("WillRun:", message, StringComparison.Ordinal);
        Assert.Contains("Basics.Tests: expected 3, was 1", message, StringComparison.Ordinal);
        Assert.Contains("Basics.Unrecognised: will run 5, not listed", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_notice_nobody_expected_fails_and_so_does_a_missing_one()
    {
        var message = Failures(Matching with { Notices = ["dialect-over-selects"] }, SelectedReport());

        Assert.Contains("Notices:", message, StringComparison.Ordinal);
        Assert.Contains("missing dialect-over-selects", message, StringComparison.Ordinal);
        Assert.Contains("not expected whole-project-fallback", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_different_set_of_reasons_fails()
    {
        var report = SelectedReport() with
        {
            Outcome = "nothing-selected",
            Reasons = ["no-changed-member-reached-a-test"],
        };

        var message = Failures(
            Matching with { Outcome = Outcome.NothingSelected, Reasons = ["changes-were-formatting-only"] },
            report);

        Assert.Contains(
            "Reasons:\n  missing changes-were-formatting-only\n  not expected no-changed-member-reached-a-test",
            message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_different_tests_reached_count_fails_and_so_does_a_change_nobody_listed()
    {
        var message = Failures(
            Matching with { Changes = new() { ["Basics.Core.Discount.Apply(decimal, int)"] = 2 } },
            SelectedReport());

        Assert.Contains("Changes:", message, StringComparison.Ordinal);
        Assert.Contains("Basics.Core.Discount.Apply(decimal, int): expected 2, was 1", message, StringComparison.Ordinal);
        Assert.Contains("Basics.Core.Basket: changed, not expected", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_different_tier_fails_and_so_does_a_listed_change_that_did_not_happen()
    {
        var message = Failures(
            Matching with
            {
                Tiers = new()
                {
                    ["Basics.Core.Discount.Apply(decimal, int)"] = Tier.WholeType,
                    ["Basics.Core.Basket"] = Tier.WholeType,
                    ["Basics.Core.Gone"] = Tier.Member,
                },
            },
            SelectedReport());

        Assert.Contains("Tiers:", message, StringComparison.Ordinal);
        Assert.Contains("Basics.Core.Discount.Apply(decimal, int): expected whole-type, was member", message, StringComparison.Ordinal);
        Assert.Contains("Basics.Core.Gone: expected, not changed", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Basics.Core.Basket:", message, StringComparison.Ordinal);
    }

    // ---- Keys --------------------------------------------------------------------------------

    [Fact]
    public void A_multi_targeted_project_is_keyed_by_name_at_framework()
    {
        var report = SelectedReport(
            Entry("tests/Multi.Tests/Multi.Tests.csproj", "net8.0", "filtered", 2, "Multi.Tests.T.A"),
            Entry("tests/Multi.Tests/Multi.Tests.csproj", "net10.0", "skip", 2));

        var expected = Matching with
        {
            Selected = new(Matching.Selected) { ["Multi.Tests@net8.0"] = ["Multi.Tests.T.A"] },
        };

        Assert.Null(Failures(expected, report));
    }

    [Fact]
    public void A_bare_name_matching_more_than_one_entry_is_an_ambiguity_error()
    {
        var report = SelectedReport(
            Entry("tests/Multi.Tests/Multi.Tests.csproj", "net8.0", "filtered", 2, "Multi.Tests.T.A"),
            Entry("tests/Multi.Tests/Multi.Tests.csproj", "net10.0", "filtered", 2, "Multi.Tests.T.A"));

        var expected = Matching with
        {
            Selected = new(Matching.Selected) { ["Multi.Tests"] = ["Multi.Tests.T.A"] },
        };

        var message = Failures(expected, report);

        Assert.Contains(
            "Multi.Tests: matches 2 entries (Multi.Tests@net10.0, Multi.Tests@net8.0); write Name@tfm",
            message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_that_matches_no_entry_fails()
    {
        var message = Failures(Matching with { RunInFull = ["Basics.Unrecognised", "Nope.Tests"] }, SelectedReport());

        Assert.Contains("RunInFull:\n  Nope.Tests: no test project in the report has this key", message, StringComparison.Ordinal);
    }

    // ---- The invariants ----------------------------------------------------------------------

    [Fact]
    public void Two_reports_that_differ_only_in_their_timings_are_deterministic()
    {
        var first = SelectedReport();
        var second = first with
        {
            Envelope = first.Envelope with
            {
                Timings = first.Envelope.Timings with { TotalMs = 999, StartedUtc = "2030-01-01T00:00:00.0000000+00:00" },
            },
        };

        Assert.Null(Expectation.Failures(Matching, Observed(first, second)));
    }

    [Fact]
    public void Two_reports_that_differ_otherwise_fail_determinism_with_a_diff()
    {
        var first = SelectedReport();
        var second = first with { Envelope = first.Envelope with { Head = "abc" } };

        var message = Expectation.Failures(Matching, Observed(first, second));

        Assert.Contains("Invariant, determinism:", message, StringComparison.Ordinal);
        Assert.Contains("+ ", message, StringComparison.Ordinal);
        Assert.Contains("\"head\": \"abc\"", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_successful_outcome_that_exits_non_zero_fails_the_exit_code_invariant()
    {
        var message = Failures(Matching, SelectedReport(), exitCode: 3);

        Assert.Contains("Invariant, exit code matches outcome:\n  run 1 exited 3 with outcome selected, which exits 0", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_outcome_must_exit_the_expected_code()
    {
        var expected = Matching with { Outcome = Outcome.Failed, ExitCode = 2, Selected = new(), RunInFull = [] };

        Assert.Null(Failures(expected, FailedReport(), exitCode: 2));

        var message = Failures(expected, FailedReport(), exitCode: 5);

        Assert.Contains("Invariant, exit code matches outcome:\n  run 1 exited 5 with outcome failed; expected 2", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_with_nothing_selected_that_is_not_run_in_full_must_be_a_skip_with_no_invocations()
    {
        var report = SelectedReport(
            Entry("tests/Empty.Tests/Empty.Tests.csproj", "net10.0", "filtered", 3),
            Entry("tests/Quiet.Tests/Quiet.Tests.csproj", "net10.0", "skip", 3) with
            {
                Invocations = [["dotnet", "test"]],
            });

        var message = Failures(Matching, report);

        Assert.Contains("Invariant, skip means nothing runs:", message, StringComparison.Ordinal);
        Assert.Contains("Empty.Tests: nothing selected, mode filtered, 1 invocation(s)", message, StringComparison.Ordinal);
        Assert.Contains("Quiet.Tests: nothing selected, mode skip, 1 invocation(s)", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_run_in_full_never_serialises_a_total_of_zero()
    {
        var report = SelectedReport() with
        {
            Entries = [.. SelectedReport().Entries.Select(entry =>
                entry.Mode == "run-all" ? entry with { Counts = new ReportCounts(0, 0, 0) } : entry)],
        };

        var message = Failures(Matching, report);

        Assert.Contains(
            "Invariant, unknown is never zero:\n  Basics.Unrecognised: runs in full, total serialised as 0",
            message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_run_in_full_may_carry_a_total_it_counted()
    {
        // A platform-suffixed project (row 10) is analysed, so its total is known, and only its
        // rendering runs it in full.
        var report = SelectedReport() with
        {
            Entries = [.. SelectedReport().Entries.Select(entry =>
                entry.Mode == "run-all" ? entry with { Counts = new ReportCounts(0, 0, 4) } : entry)],
        };

        Assert.Null(Failures(Matching, report));
    }

    [Fact]
    public void When_the_harness_build_failed_the_failure_carries_its_tail()
    {
        var observed = Observed(SelectedReport(), SelectedReport()) with { BuildFailure = "error CS1002: ; expected" };

        var message = Expectation.Failures(Matching with { Outcome = Outcome.NoChanges }, observed);

        Assert.Contains("error CS1002: ; expected", message, StringComparison.Ordinal);
        Assert.Null(Expectation.Failures(Matching, observed));
    }

    // ---- Helpers -----------------------------------------------------------------------------

    private static string? Failures(Expected expected, Report report, int exitCode = 0) =>
        Expectation.Failures(expected, Observed(report, report, exitCode));

    private static Observation Observed(Report first, Report second, int exitCode = 0) =>
        new(new ReachRun(exitCode, first), new ReachRun(exitCode, second), TempRoot, BuildFailure: null);

    /// <summary>
    /// A selected run: one filtered xUnit project, one unrecognised project run in full.
    /// </summary>
    private static Report SelectedReport(params ReportEntry[] more) =>
        ReportFixture.Minimal() with
        {
            Outcome = "selected",
            Changes =
            [
                new ReportChange(0, "Basics.Core.Basket", "whole-type", "moved", 0),
                new ReportChange(1, "Basics.Core.Discount.Apply(decimal, int)", "member", "body", 1),
            ],
            Entries =
            [
                Entry(
                    "tests/Basics.Tests/Basics.Tests.csproj",
                    "net10.0",
                    "filtered",
                    3,
                    "Basics.Tests.DiscountTests.Ten_percent_off_a_hundred_is_ninety"),
                Entry("tests/Basics.Unrecognised/Basics.Unrecognised.csproj", "net10.0", "run-all", null) with
                {
                    Counts = new ReportCounts(0, 5, null),
                },
                .. more,
            ],
            Notices =
            [
                new ReportNotice { Code = "whole-project-fallback", Kind = "blind-spot", Message = "runs in full" },
            ],
        };

    private static Report FailedReport() =>
        ReportFixture.Minimal() with { Outcome = "failed" };

    private static ReportEntry Entry(string project, string framework, string mode, int? total, params string[] tests) =>
        new()
        {
            Project = project,
            TargetFramework = framework,
            Mode = mode,
            Counts = new ReportCounts(tests.Length, tests.Length, total),
            Tests =
            [
                .. tests.Select(test => new ReportTest
                {
                    Display = test,
                    Id = test,
                    Rules = ["reverse-reachable"],
                    Changes = [1],
                })
            ],
            Invocations = mode == "skip" ? [] : [["dotnet", "test", "--project", project]],
        };
}
