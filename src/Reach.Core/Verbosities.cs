namespace Reach;

public static class Verbosities
{
    /// <summary>
    /// Every spelling <c>dotnet build -v</c> accepts, long and short.
    /// </summary>
    public static IReadOnlyList<string> Spellings { get; } =
        ["q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic"];

    private static readonly Dictionary<string, Verbosity> BySpelling = new(StringComparer.OrdinalIgnoreCase)
    {
        ["q"] = Verbosity.Quiet,
        ["quiet"] = Verbosity.Quiet,
        ["m"] = Verbosity.Minimal,
        ["minimal"] = Verbosity.Minimal,
        ["d"] = Verbosity.Detailed,
        ["detailed"] = Verbosity.Detailed,
        ["diag"] = Verbosity.Diagnostic,
        ["diagnostic"] = Verbosity.Diagnostic,
    };

    /// <summary>
    /// Any spelling, in any case. Anything unrecognised, <c>n</c> and <c>normal</c> included, is normal.
    /// </summary>
    public static Verbosity Parse(string? value) =>
        value is not null && BySpelling.TryGetValue(value, out var verbosity) ? verbosity : Verbosity.Normal;
}
