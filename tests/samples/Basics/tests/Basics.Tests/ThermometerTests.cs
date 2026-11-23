using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class ThermometerTests
{
    [Fact]
    public void Freezing_is_thirty_two_fahrenheit() => Assert.Equal(32m, Thermometer.ToFahrenheit(Readings.Freezing()));

    [Fact]
    public void Freezing_is_273_kelvin() => Assert.Equal(273.15m, Thermometer.ToKelvin(Readings.Freezing()));

    [Fact]
    public void A_hundred_celsius_is_212_fahrenheit() => Assert.Equal(212m, Thermometer.ToFahrenheit(100m));
}
