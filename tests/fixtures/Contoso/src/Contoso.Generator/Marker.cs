namespace Contoso.Generation;

/// <summary>
/// A stand-in. What matters for the fixture is how this project is *referenced*, not what it
/// generates — Reach never runs a generator.
/// </summary>
public static class Marker
{
    public static string Name => "contoso";
}
