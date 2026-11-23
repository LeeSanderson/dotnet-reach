using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class MileageTests
{
    [Fact]
    public void Five_hundred_kilometres_on_forty_litres_is_twelve_and_a_half() =>
        Assert.Equal(12.5m, Mileage.KilometresPerLitre(500m, 40m));
}
