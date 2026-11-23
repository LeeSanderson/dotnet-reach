namespace Basics.Core;

/// <summary>
/// Row 5: the conversions its tests check. Row 5's patch deletes a method of the tests' own
/// <c>Readings</c> helper, not of this type.
/// </summary>
public static class Thermometer
{
    private const decimal KelvinOffset = 273.15m;
    private const decimal FahrenheitOffset = 32m;
    private const decimal FahrenheitPerCelsius = 1.8m;

    public static decimal ToFahrenheit(decimal celsius) => (celsius * FahrenheitPerCelsius) + FahrenheitOffset;

    public static decimal ToKelvin(decimal celsius) => celsius + KelvinOffset;
}
