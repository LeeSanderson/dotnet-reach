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
}
