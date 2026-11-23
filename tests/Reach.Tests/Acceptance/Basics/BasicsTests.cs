namespace Reach.Tests.Acceptance.Basics;

/// <summary>
/// The acceptance tests over the Basics sample, <c>tests/samples/Basics/</c>.
/// </summary>
public sealed class BasicsTests() : SampleTests("Basics")
{
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
