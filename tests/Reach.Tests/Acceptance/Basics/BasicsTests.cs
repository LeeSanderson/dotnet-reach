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
        "Basics.Tests.GreetingTests.Goodbye_names_the_person",
        "Basics.Tests.GreetingTests.Hello_names_the_person",
        "Basics.Tests.HandlerTests.A_bar_handler_describes_a_bar",
        "Basics.Tests.HandlerTests.A_foo_handler_describes_a_foo",
        "Basics.Tests.InitialsTests.A_single_name_gives_one_initial",
        "Basics.Tests.InitialsTests.Initials_take_the_first_letter_of_each_name",
        "Basics.Tests.LabelTests.A_bracket_label_prints_in_brackets",
        "Basics.Tests.LabelTests.An_upper_label_called_directly_renders_in_capitals",
        "Basics.Tests.LabelTests.An_upper_label_prints_in_capitals",
        "Basics.Tests.MileageTests.Five_hundred_kilometres_on_forty_litres_is_twelve_and_a_half",
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
        "Basics.Tests.VowelsTests.Banana_has_three_vowels",
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
    public async Task A_multi_targeted_test_project_gives_one_entry_per_framework() =>
        await Baseline("multi-targeted.patch").Apply("greeting-hello-change.patch").ExpectAsync(new()
        {
            Because = "Only Greeting.Hello changed and one test calls it, and Basics.Tests builds for "
                + "net8.0 and net10.0, so that test is selected once per framework, in an entry of its own.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Basics.Tests@net8.0"] = ["Basics.Tests.GreetingTests.Hello_names_the_person"],
                ["Basics.Tests@net10.0"] = ["Basics.Tests.GreetingTests.Hello_names_the_person"],
            },
            RunInFull = [],
        });

    [Fact]
    public async Task A_platform_suffixed_framework_makes_its_project_run_in_full() =>
        await Baseline("platform-suffixed.patch").Apply("mileage-change.patch").ExpectAsync(new()
        {
            Because = "Only Mileage changed, but Basics.Tests also targets net10.0-windows, whose "
                + "framework selector cannot be recovered from metadata, so the whole project runs "
                + "on both frameworks instead of the one test that calls Mileage.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Basics.Tests@net10.0"] = ["Basics.Tests.MileageTests.Five_hundred_kilometres_on_forty_litres_is_twelve_and_a_half"],
                ["Basics.Tests@net10.0-windows"] = ["Basics.Tests.MileageTests.Five_hundred_kilometres_on_forty_litres_is_twelve_and_a_half"],
            },
            RunInFull = ["Basics.Tests@net10.0", "Basics.Tests@net10.0-windows"],
            Notices = ["baseline-resolved", "baseline-is-head", "framework-selector-underivable"],
        });

    [Fact]
    public async Task An_xunit_v3_4_project_without_the_runner_setting_gets_its_notice() =>
        await Baseline("no-global-json.patch").Apply("vowels-change.patch").ExpectAsync(new()
        {
            Because = "Only Vowels changed and one test calls it; with global.json gone, nothing "
                + "tells dotnet test that xunit.v3 4.0.0 runs on Microsoft.Testing.Platform, so the "
                + "selection is unchanged and the missing setting is reported.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Basics.Tests"] = ["Basics.Tests.VowelsTests.Banana_has_three_vowels"],
            },
            RunInFull = [],
            Notices = ["baseline-resolved", "baseline-is-head", "test-runner-not-configured"],
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
