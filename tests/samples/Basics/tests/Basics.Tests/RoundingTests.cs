using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class RoundingTests
{
    [Fact]
    public void A_whole_amount_is_unchanged() => Assert.Equal(12m, Rounding.ToCents(12m));
}
