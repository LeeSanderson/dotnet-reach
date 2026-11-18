namespace Reach;

internal static class Verbosities
{
    /// <summary>
    /// Every spelling <c>dotnet build -v</c> accepts, long and short.
    /// </summary>
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
