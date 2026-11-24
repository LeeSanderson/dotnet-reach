namespace Reach.Tests.Acceptance.Frameworks;

/// <summary>
/// The acceptance tests over the Frameworks sample, <c>tests/samples/Frameworks/</c>: one test
/// project per dialect, all calling the same production code.
/// </summary>
public sealed class FrameworksTests() : SampleTests("Frameworks")
{
    /// <summary>
    /// The patch row 7 applies, and the one <c>ExampleReportTests</c> applies, so the example and
    /// a gate test cannot drift apart.
    /// </summary>
    internal const string TaxChange = "tax-rounds-half-up.patch";

    /// <summary>
    /// The notices every Frameworks run carries: those of every run, and the standing notices
    /// the NUnit and unrecognised projects bring, whatever the patch.
    /// </summary>
    private static readonly string[] StandingNotices =
    [
        .. EveryRunNotices,
        "nunit-filter-overmatch",
        "whole-project-fallback",
    ];

    [Fact]
    public async Task Each_dialect_renders_its_own_filter_and_NUnit_runs_more_than_it_selects() =>
        await Apply(TaxChange).ExpectAsync(new()
        {
            Because = "Only Invoice.Tax changed: in every recognised project the test that totals "
                + "an invoice reaches it and the one that calls Untouched does not, the "
                + "unrecognised project runs in full, and NUnit's ~MyTest filter also matches "
                + "MyTest2.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Frameworks.Tests.MSTest"] = ["Frameworks.Tests.MSTest.InvoiceTests.Totals_include_tax"],
                ["Frameworks.Tests.NUnit"] = ["Frameworks.Tests.NUnit.InvoiceTests.MyTest"],
                ["Frameworks.Tests.XunitV2"] = ["Frameworks.Tests.XunitV2.InvoiceTests.Totals_include_tax"],
                ["Frameworks.Tests.XunitV3"] = ["Frameworks.Tests.XunitV3.InvoiceTests.Totals_include_tax"],
                ["Frameworks.Tests.XunitV3_4"] = ["Frameworks.Tests.XunitV3_4.InvoiceTests.Totals_include_tax"],
            },
            RunInFull = ["Frameworks.Tests.Unrecognised"],
            WillRun = new()
            {
                ["Frameworks.Tests.MSTest"] = 1,
                ["Frameworks.Tests.NUnit"] = 2,
                ["Frameworks.Tests.XunitV2"] = 1,
                ["Frameworks.Tests.XunitV3"] = 1,
                ["Frameworks.Tests.XunitV3_4"] = 1,
            },
            Notices = [.. StandingNotices, "dialect-over-selects"],
        });

    [Fact]
    public async Task A_project_on_an_unrecognised_framework_runs_in_full_with_an_unknown_total() =>
        await Apply("unrecognised-check-message.patch").ExpectAsync(new()
        {
            Because = "Only a check in the unrecognised project changed, which no recognised "
                + "test reaches, and that project cannot be enumerated, so it runs in full and its "
                + "total is unknown rather than zero.",
            Outcome = Outcome.Selected,
            Selected = [],
            RunInFull = ["Frameworks.Tests.Unrecognised"],
            UnknownTotal = ["Frameworks.Tests.Unrecognised"],
            Notices = StandingNotices,
        });
}
