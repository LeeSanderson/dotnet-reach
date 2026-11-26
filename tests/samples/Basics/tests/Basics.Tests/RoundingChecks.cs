using Basics.Core;
using Xunit;

namespace Basics.Tests;

/// <summary>
/// Row 3: an existing method that is not a test. Row 3's patch gives it <c>[Fact]</c> and
/// leaves its body alone.
/// </summary>
public class RoundingChecks
{
    private readonly decimal _halfCentOverTwelve = 0.125m;

    public void A_half_cent_rounds_to_even() => Assert.Equal(0.12m, Rounding.ToCents(_halfCentOverTwelve));
}
