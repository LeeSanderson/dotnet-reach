using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class BasketTests
{
    [Fact]
    public void A_discounted_basket_totals_less_than_its_prices()
    {
        var basket = new Basket(discountPercent: 50);

        basket.Add(30m);
        basket.Add(10m);

        Assert.Equal(20m, basket.Total());
    }
}
