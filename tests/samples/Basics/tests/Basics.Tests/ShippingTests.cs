using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class ShippingTests
{
    [Fact]
    public void A_flat_rate_quote_ignores_weight() => Assert.Equal(5m, Shipping.Quote(new FlatRate(), 3m));

    [Fact]
    public void A_weight_rate_quote_grows_with_weight() => Assert.Equal(6m, Shipping.Quote(new WeightRate(), 3m));

    [Fact]
    public void A_weight_rate_called_directly_charges_per_kilo() => Assert.Equal(4m, new WeightRate().Cost(2m));
}
