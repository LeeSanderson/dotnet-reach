namespace Reach;

/// <summary>
/// Mirrors the SDK's <c>quiet|minimal|normal|detailed|diagnostic</c>, spelling included, so a
/// pipeline author transfers what they already know.
/// </summary>
internal enum Verbosity
{
    Quiet,
    Minimal,
    Normal,
    Detailed,
    Diagnostic,
}

internal static class Verbosities
{
    /// <summary>Every spelling <c>dotnet build -v</c> accepts, long and short.</summary>
    internal static IReadOnlyList<string> Spellings { get; } =
        ["q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic"];

    internal static Verbosity Parse(string? value) => value?.ToLowerInvariant() switch
    {
        "q" or "quiet" => Verbosity.Quiet,
        "m" or "minimal" => Verbosity.Minimal,
        "d" or "detailed" => Verbosity.Detailed,
        "diag" or "diagnostic" => Verbosity.Diagnostic,
        _ => Verbosity.Normal,
    };
}
