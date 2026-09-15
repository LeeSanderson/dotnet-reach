namespace Reach.Output;

/// <summary>
/// The <c>--</c> boundary. Everything after it is forwarded verbatim to <c>dotnet build</c>,
/// except the switches Reach owns.
/// </summary>
/// <remarks>
/// Without a passthrough, every adopter who builds with <c>-p:</c> properties,
/// <c>--no-restore</c> or a binlog is pushed onto <c>--no-build</c> permanently, quietly
/// making the documented default the minority path.
/// </remarks>
internal static class ForwardedBuildArguments
{
    /// <summary>
    /// Each owned switch, with the Reach option that replaces it. A forwarded <c>-o</c>
    /// changes the output layout through a channel Reach never read, so it is refused rather
    /// than honoured.
    /// </summary>
    private static readonly (string Switch, string Instead)[] Owned =
    [
        ("-o", "--output"),
        ("--output", "--output"),
        ("--artifacts-path", "--artifacts-path"),
        ("-c", "--configuration"),
        ("--configuration", "--configuration"),
    ];

    /// <summary>
    /// Splits the argument vector at the first <c>--</c>. Reach does this itself rather than
    /// leaving it to the parser, whose positional argument would otherwise swallow the first
    /// forwarded token when no target was given.
    /// </summary>
    internal static (IReadOnlyList<string> Reach, IReadOnlyList<string> Build) Split(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] == "--")
            {
                return ([.. arguments.Take(index)], [.. arguments.Skip(index + 1)]);
            }
        }

        return (arguments, []);
    }

    /// <summary>The usage-error message, or <c>null</c> when every forwarded token is the build's business.</summary>
    internal static string? Refuse(IReadOnlyList<string> forwarded)
    {
        foreach (var token in forwarded)
        {
            var match = Owned.FirstOrDefault(owned => Matches(token, owned.Switch));

            if (match.Switch is not null)
            {
                return $"'{match.Switch}' changes where the build writes its output, and Reach "
                    + "reads that layout. Give it to Reach directly — "
                    + $"`reach select {match.Instead} <value>` — rather than forwarding it after '--'.";
            }
        }

        return null;
    }

    /// <summary>Matches <c>--output x</c>, <c>--output=x</c> and <c>--output:x</c> alike.</summary>
    private static bool Matches(string token, string owned) =>
        token.Equals(owned, StringComparison.Ordinal)
        || (token.StartsWith(owned, StringComparison.Ordinal)
            && token.Length > owned.Length
            && (token[owned.Length] == '=' || token[owned.Length] == ':'));
}
