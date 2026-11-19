namespace Reach;

/// <summary>
/// The spellings of <see cref="Verbosity"/> that <c>dotnet build -v</c> accepts, and how each one
/// reads. A spelling is added in one place and both the accepted list and the parser follow.
/// </summary>
public static class Verbosities
{
    /// <summary>
    /// Every spelling, long and short, in the order the help text lists them.
    /// </summary>
    private static readonly KeyValuePair<string, Verbosity>[] Known =
    [
        new("q", Verbosity.Quiet),
        new("quiet", Verbosity.Quiet),
        new("m", Verbosity.Minimal),
        new("minimal", Verbosity.Minimal),
        new("n", Verbosity.Normal),
        new("normal", Verbosity.Normal),
        new("d", Verbosity.Detailed),
        new("detailed", Verbosity.Detailed),
        new("diag", Verbosity.Diagnostic),
        new("diagnostic", Verbosity.Diagnostic),
    ];

    private static readonly Dictionary<string, Verbosity> BySpelling = new(Known, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Every spelling <c>dotnet build -v</c> accepts, long and short.
    /// </summary>
    public static IReadOnlyList<string> Spellings { get; } = [.. Known.Select(spelling => spelling.Key)];

    /// <summary>
    /// Any spelling, in any case. Anything unrecognised is normal.
    /// </summary>
    public static Verbosity Parse(string? value) =>
        value is not null && BySpelling.TryGetValue(value, out var verbosity) ? verbosity : Verbosity.Normal;
}
