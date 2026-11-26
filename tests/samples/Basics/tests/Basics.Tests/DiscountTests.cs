using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class DiscountTests
{
    [Fact]
    public void Ten_percent_off_a_hundred_is_ninety() => Assert.Equal(90m, Discount.Apply(100m, 10));

    [Fact]
    public void A_percent_over_a_hundred_is_invalid() => Assert.False(Discount.IsValid(101));
}
