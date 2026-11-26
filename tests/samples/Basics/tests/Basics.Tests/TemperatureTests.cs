using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class TemperatureTests
{
    [Fact]
    public void A_forecast_interpolates_the_temperature() =>
        Assert.Equal("Tomorrow: 21 C", $"Tomorrow: {new Temperature(21)}");
}
