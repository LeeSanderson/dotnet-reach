namespace Reach.Tests.Acceptance.Basics;

/// <summary>
/// The acceptance tests over the Basics sample, <c>tests/samples/Basics/</c>.
/// </summary>
public sealed class BasicsTests() : SampleTests("Basics")
{
    /// <summary>
    /// The two notices every run carries: the harness passes the baseline commit as
    /// <c>--base</c>, and leaves its change patches uncommitted, so <c>HEAD</c> is the baseline.
    /// </summary>
    private static readonly string[] EveryRunNotices = ["baseline-resolved", "baseline-is-head"];

    /// <summary>
    /// Every test in <c>Basics.Tests</c>, for the rows whose patch widens the whole project. A
    /// new sample test belongs here too.
    /// </summary>
    private static readonly IReadOnlyList<string> EveryBasicsTest =
    [
        "Basics.Tests.BasketTests.A_discounted_basket_totals_less_than_its_prices",
        "Basics.Tests.DiscountTests.A_percent_over_a_hundred_is_invalid",
        "Basics.Tests.DiscountTests.Ten_percent_off_a_hundred_is_ninety",
        "Basics.Tests.HandlerTests.A_bar_handler_describes_a_bar",
        "Basics.Tests.HandlerTests.A_foo_handler_describes_a_foo",
        "Basics.Tests.InitialsTests.A_single_name_gives_one_initial",
        "Basics.Tests.InitialsTests.Initials_take_the_first_letter_of_each_name",
        "Basics.Tests.LabelTests.A_bracket_label_prints_in_brackets",
        "Basics.Tests.LabelTests.An_upper_label_called_directly_renders_in_capitals",
        "Basics.Tests.LabelTests.An_upper_label_prints_in_capitals",
        "Basics.Tests.RoundingTests.A_whole_amount_is_unchanged",
        "Basics.Tests.ShippingTests.A_flat_rate_quote_ignores_weight",
        "Basics.Tests.ShippingTests.A_weight_rate_called_directly_charges_per_kilo",
        "Basics.Tests.ShippingTests.A_weight_rate_quote_grows_with_weight",
        "Basics.Tests.TallyTests.A_new_tally_counts_zero",
        "Basics.Tests.TallyTests.Three_adds_count_three",
        "Basics.Tests.TemperatureTests.A_forecast_interpolates_the_temperature",
        "Basics.Tests.ThermometerTests.A_hundred_celsius_is_212_fahrenheit",
        "Basics.Tests.ThermometerTests.Freezing_is_273_kelvin",
        "Basics.Tests.ThermometerTests.Freezing_is_thirty_two_fahrenheit",
    ];

    [Fact]
    public async Task A_change_selects_exactly_the_tests_that_reach_it_through_compiled_edges() =>
        await Apply("discount-body-change.patch").ExpectAsync(new()
        {
            Because = "Only Discount.Apply's body changed: one test calls it, another reaches it "
                + "through Basket.Total, and the validity test calls only IsValid.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Basics.Tests"] =
                [
                    "Basics.Tests.BasketTests.A_discounted_basket_totals_less_than_its_prices",
                    "Basics.Tests.DiscountTests.Ten_percent_off_a_hundred_is_ninety",
                ],
            },
            RunInFull = [],
        });

    [Fact]
    public async Task A_test_whose_own_body_changed_is_selected() =>
        await Apply("test-body-change.patch").ExpectAsync(new()
        {
            Because = "Only that test's own body changed, and Initials, which it calls, did not, "
                + "so its sibling in the same class is not selected.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Basics.Tests"] = ["Basics.Tests.InitialsTests.Initials_take_the_first_letter_of_each_name"],
            },
            RunInFull = [],
        });

    [Fact]
    public async Task An_existing_method_that_gains_Fact_is_selected() =>
        await Apply("fact-added-to-an-existing-method.patch").ExpectAsync(new()
        {
            Because = "Only the attribute was added, to a method whose body is unchanged, so that "
                + "method is the one new test, and the existing test of Rounding is not selected.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Basics.Tests"] = ["Basics.Tests.RoundingChecks.A_half_cent_rounds_to_even"],
            },
            RunInFull = [],
        });

    [Fact]
    public async Task Interface_and_virtual_dispatch_select_every_candidate() =>
        await Apply("dispatch-implementation-changes.patch").ExpectAsync(new()
        {
            Because = "FlatRate.Cost and BracketLabel.Render changed; every test that calls "
                + "through IShippingRate or Label could reach them, whichever implementation it "
                + "passes, while the tests that call WeightRate and UpperLabel directly cannot.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Basics.Tests"] =
                [
                    "Basics.Tests.LabelTests.A_bracket_label_prints_in_brackets",
                    "Basics.Tests.LabelTests.An_upper_label_prints_in_capitals",
                    "Basics.Tests.ShippingTests.A_flat_rate_quote_ignores_weight",
                    "Basics.Tests.ShippingTests.A_weight_rate_quote_grows_with_weight",
                ],
            },
            RunInFull = [],
            Tiers = new()
            {
                ["Basics.Core.BracketLabel.Render(string)"] = Tier.Member,
                ["Basics.Core.FlatRate.Cost(decimal)"] = Tier.Member,
            },
        });

    [Fact]
    public async Task A_deleted_method_widens_at_least_its_declaring_type() =>
        await Apply("method-deleted.patch").ExpectAsync(new()
        {
            // Pinned, not intended: the deleted member cannot be matched to compiled code, so it
            // also falls through to whole-assembly widening of Basics.Tests. Without that, only
            // the two tests that read Readings.Freezing would be selected. See ticket 06.
            Because = "Deleting Readings.BodyHeat widens the whole of Readings, and the deleted "
                + "member, which matches no compiled code, widens all of Basics.Tests, so every "
                + "Basics test is selected.",
            Outcome = Outcome.Selected,
            Selected = new() { ["Basics.Tests"] = EveryBasicsTest },
            RunInFull = [],
            Tiers = new()
            {
                ["Basics.Tests.Readings"] = Tier.WholeType,
                ["Basics.Tests.Readings.BodyHeat()"] = Tier.WholeAssembly,
            },
        });

    [Fact]
    public async Task A_move_to_another_directory_in_the_same_namespace_widens_that_type() =>
        await Apply("type-moved-to-another-directory.patch").ExpectAsync(new()
        {
            Because = "Tally's file moved and its code did not, so no member changed and the whole "
                + "of Tally counts as changed, which selects both of its tests and nothing else.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Basics.Tests"] =
                [
                    "Basics.Tests.TallyTests.A_new_tally_counts_zero",
                    "Basics.Tests.TallyTests.Three_adds_count_three",
                ],
            },
            RunInFull = [],
            Tiers = new() { ["Basics.Core.Tally"] = Tier.WholeType },
        });

    [Fact]
    public async Task A_generator_project_is_in_scope_and_a_change_to_it_widens_its_consumers() =>
        await Apply("generator-project-change.patch").ExpectAsync(new()
        {
            Because = "Only the generator's project file changed, and Basics.Core consumes it as an analyser, so "
                + "Basics.Core widens and every test reaching it is selected, which is every Basics test; had "
                + "Reach expected the generator on disk, the run would end failed instead.",
            Outcome = Outcome.Selected,
            Selected = new() { ["Basics.Tests"] = EveryBasicsTest },
            RunInFull = [],
        });

    [Fact]
    public async Task A_formatting_only_change_is_no_changes() =>
        await Apply("receipt-formatting-only.patch").ExpectAsync(new()
        {
            Because = "Receipt gained a comment and a space, which changes no member and leaves nothing to "
                + "widen, so Reach reports no changes rather than nothing selected, with no reason code.",
            Outcome = Outcome.NoChanges,
            Selected = [],
            RunInFull = [],
            Notices = EveryRunNotices,
            Reasons = [],
        });

    [Fact]
    public async Task A_docs_only_change_selects_nothing_and_says_no_member_changed() =>
        await Apply("readme-only.patch").ExpectAsync(new()
        {
            Because = "Only the sample's README changed, which sits in no project, so Reach routes it to no "
                + "project, flags it, and selects nothing because no member changed.",
            Outcome = Outcome.NothingSelected,
            Selected = [],
            RunInFull = [],
            Notices = [.. EveryRunNotices, "unmapped-file-no-project"],
            Reasons = ["changes-were-formatting-only"],
        });

    [Fact]
    public async Task A_changed_member_that_reaches_no_test_is_listed_with_a_count_of_zero() =>
        await Apply("receipt-header-change.patch").ExpectAsync(new()
        {
            Because = "Only Receipt.Header's body changed, and no test calls it, so it is listed as a change "
                + "that reached no test.",
            Outcome = Outcome.NothingSelected,
            Selected = [],
            RunInFull = [],
            Notices = EveryRunNotices,
            Reasons = ["no-changed-member-reached-a-test"],
            Changes = new() { ["Basics.Core.Receipt.Header(string)"] = 0 },
        });

    [Fact]
    public async Task A_new_source_file_not_tracked_by_git_is_flagged() =>
        await Apply("voucher-new-file.patch").ExpectAsync(new()
        {
            Because = "The patch adds Voucher.cs, which the harness leaves untracked; Reach includes it and says "
                + "so, and no test reaches the new type.",
            Outcome = Outcome.NothingSelected,
            Selected = [],
            RunInFull = [],
            Notices = [.. EveryRunNotices, "untracked-source-files"],
        });

    [Fact]
    public async Task A_change_that_breaks_the_build_fails_with_exit_code_2() =>
        await Apply("receipt-breaks-the-build.patch").ExpectAsync(new()
        {
            Because = "Receipt.Header now returns an int where a string is declared, so the build fails and "
                + "Reach, building for itself, reports the failure.",
            Outcome = Outcome.Failed,
            Selected = [],
            RunInFull = [],
            ExitCode = 2,
        });

    [Fact]
    public async Task A_change_only_to_a_project_no_test_reaches_selects_nothing() =>
        await Apply("tools-only-change.patch").ExpectAsync(new()
        {
            Because = "Only Basics.Tools changed, and no test project references it, so its changed member is "
                + "listed as reaching no test.",
            Outcome = Outcome.NothingSelected,
            Selected = [],
            RunInFull = [],
            Notices = EveryRunNotices,
            Reasons = ["no-changed-member-reached-a-test"],
            Changes = new() { ["Basics.Tools.Housekeeping.DaysToKeep(int)"] = 0 },
        });
    [Fact]
    [KnownGap]
    public async Task Known_gap_a_change_reached_only_through_Handler_of_Foo_selects_the_users_of_Handler_of_Bar() =>
        await Apply("foo-describe-change.patch").ExpectAsync(new()
        {
            Because = "Only Foo.Describe changed, which only Handler<Foo> reaches, but Handler<Bar>'s test is "
                + "selected too: docs/limitations.md, \"Generic instantiations are collapsed\" (ADR-0006).",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Basics.Tests"] =
                [
                    "Basics.Tests.HandlerTests.A_bar_handler_describes_a_bar",
                    "Basics.Tests.HandlerTests.A_foo_handler_describes_a_foo",
                ],
            },
            RunInFull = [],
            Changes = new() { ["Basics.Core.Foo.Describe()"] = 2 },
        });

    [Fact]
    [KnownGap]
    public async Task Known_gap_a_ToString_reached_only_through_interpolation_selects_nothing() =>
        await Apply("temperature-tostring-change.patch").ExpectAsync(new()
        {
            Because = "Only Temperature.ToString changed, and its one test reaches it only through string "
                + "interpolation, which never names ToString in IL: docs/limitations.md, \"First-party members "
                + "invoked only from outside the analysis scope\".",
            Outcome = Outcome.NothingSelected,
            Selected = [],
            RunInFull = [],
            Changes = new() { ["Basics.Core.Temperature.ToString()"] = 0 },
        });
}
