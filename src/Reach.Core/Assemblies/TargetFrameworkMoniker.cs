namespace Reach.Assemblies;

/// <summary>
/// A target framework in the one form both sides can be compared in: the project file declares
/// <c>net10.0-windows</c>, the assembly stamps <c>.NETCoreApp,Version=v10.0</c> plus
/// <c>Windows7.0</c>, and neither spelling can be turned into the other.
/// </summary>
/// <param name="Platform">Lower-cased and with its version stripped: <c>windows</c>, not <c>Windows7.0</c>.</param>
internal sealed record TargetFrameworkMoniker(string Identifier, Version Version, string? Platform)
{
    private const string NetCoreApp = ".NETCoreApp";
    private const string NetFramework = ".NETFramework";
    private const string NetStandard = ".NETStandard";

    /// <summary>Parses a declared moniker — <c>net10.0</c>, <c>net10.0-windows</c>, <c>netstandard2.0</c>, <c>net472</c>.</summary>
    internal static TargetFrameworkMoniker? Parse(string declared)
    {
        if (string.IsNullOrWhiteSpace(declared))
        {
            return null;
        }

        var moniker = declared.Trim().ToLowerInvariant();
        var dash = moniker.IndexOf('-');
        var platform = dash < 0 ? null : StripVersion(moniker[(dash + 1)..]);

        if (dash >= 0)
        {
            moniker = moniker[..dash];
        }

        if (moniker.StartsWith("netstandard", StringComparison.Ordinal))
        {
            return Build(NetStandard, moniker["netstandard".Length..], platform);
        }

        if (moniker.StartsWith("netcoreapp", StringComparison.Ordinal))
        {
            return Build(NetCoreApp, moniker["netcoreapp".Length..], platform);
        }

        if (!moniker.StartsWith("net", StringComparison.Ordinal))
        {
            return null;
        }

        var rest = moniker["net".Length..];

        // net10.0 is .NET; net472 is .NET Framework. The dot is the whole distinction, and it
        // is the SDK's own rule rather than a heuristic.
        return rest.Contains('.', StringComparison.Ordinal)
            ? Build(NetCoreApp, rest, platform)
            : Build(NetFramework, string.Join('.', rest.ToCharArray()), platform);
    }

    /// <summary>Reads the attributes an assembly carries.</summary>
    internal static TargetFrameworkMoniker? FromAttributes(string? frameworkName, string? targetPlatform)
    {
        if (string.IsNullOrWhiteSpace(frameworkName))
        {
            return null;
        }

        var parts = frameworkName.Split(',');
        var version = parts
            .Select(part => part.Trim())
            .FirstOrDefault(part => part.StartsWith("Version=", StringComparison.OrdinalIgnoreCase));

        if (version is null)
        {
            return null;
        }

        return Build(
            parts[0].Trim(),
            version["Version=".Length..].TrimStart('v', 'V'),
            targetPlatform is null ? null : StripVersion(targetPlatform.ToLowerInvariant()));
    }

    /// <summary>
    /// Two monikers are the same assembly instance when identifier, version and platform all
    /// agree. The platform half is what keeps <c>net10.0</c> and <c>net10.0-windows</c> apart.
    /// </summary>
    internal bool Matches(TargetFrameworkMoniker other) =>
        string.Equals(Identifier, other.Identifier, StringComparison.OrdinalIgnoreCase)
        && Version == other.Version
        && string.Equals(Platform, other.Platform, StringComparison.OrdinalIgnoreCase);

    public override string ToString() =>
        Platform is null
            ? $"{Identifier},Version=v{Version.ToString(2)}"
            : $"{Identifier},Version=v{Version.ToString(2)} ({Platform})";

    private static TargetFrameworkMoniker? Build(string identifier, string version, string? platform) =>
        Normalise(version) is { } parsed
            ? new TargetFrameworkMoniker(identifier, parsed, string.IsNullOrEmpty(platform) ? null : platform)
            : null;

    /// <summary>
    /// <c>10.0</c>, <c>v10.0</c> and <c>10.0.0.0</c> are one version. Without this the
    /// declared moniker and the attribute would never compare equal.
    /// </summary>
    private static Version? Normalise(string version) =>
        Version.TryParse(version.Contains('.', StringComparison.Ordinal) ? version : version + ".0", out var parsed)
            ? new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0))
            : null;

    /// <summary><c>windows7.0</c> and <c>windows</c> are the same platform.</summary>
    private static string StripVersion(string platform)
    {
        var index = platform.AsSpan().IndexOfAnyInRange('0', '9');

        return (index < 0 ? platform : platform[..index]).Trim();
    }
}
