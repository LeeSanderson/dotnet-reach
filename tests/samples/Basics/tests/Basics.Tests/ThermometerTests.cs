using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class ThermometerTests
{
    [Fact]
    public void Freezing_is_thirty_two_fahrenheit() => Assert.Equal(32m, Thermometer.ToFahrenheit(0m));

    [Fact]
    public void Freezing_is_273_kelvin() => Assert.Equal(273.15m, Thermometer.ToKelvin(0m));
}
