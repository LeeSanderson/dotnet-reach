namespace Reach.Tests.Acceptance.Basics;

/// <summary>
/// The acceptance tests over the Basics sample, <c>tests/samples/Basics/</c>.
/// </summary>
public sealed class BasicsTests() : SampleTests("Basics")
{
    /// <summary>
    /// Every test in <c>Basics.Tests</c>, for the rows whose patch widens the whole project. A
    /// new sample test belongs here too.
    /// </summary>
    private static readonly IReadOnlyList<string> EveryBasicsTest =
    [
        "Basics.Tests.BasketTests.A_discounted_basket_totals_less_than_its_prices",
        "Basics.Tests.DiscountTests.A_percent_over_a_hundred_is_invalid",
        "Basics.Tests.DiscountTests.Ten_percent_off_a_hundred_is_ninety",
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
}
