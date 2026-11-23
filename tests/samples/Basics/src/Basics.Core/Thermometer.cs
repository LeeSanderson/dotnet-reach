namespace Basics.Core;

/// <summary>
/// Row 5's subject: a method no test calls is deleted, which widens the whole type. Only
/// row 5's patch touches it.
/// </summary>
public static class Thermometer
{
    private const decimal KelvinOffset = 273.15m;
    private const decimal FahrenheitOffset = 32m;
    private const decimal FahrenheitPerCelsius = 1.8m;

    public static decimal ToFahrenheit(decimal celsius) => (celsius * FahrenheitPerCelsius) + FahrenheitOffset;

    public static decimal ToKelvin(decimal celsius) => celsius + KelvinOffset;

    public static decimal ToRankine(decimal celsius) => ToKelvin(celsius) * FahrenheitPerCelsius;
}
