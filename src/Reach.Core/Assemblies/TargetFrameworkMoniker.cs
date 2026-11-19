using System.Collections.Frozen;

namespace Reach.Assemblies;

/// <summary>
/// A target framework in the one form both sides can be compared in: the project file declares
/// <c>net10.0-windows</c>, the assembly stamps <c>.NETCoreApp,Version=v10.0</c> plus
/// <c>Windows7.0</c>, and neither spelling can be turned into the other.
/// </summary>
/// <param name="Identifier">The framework family as assemblies spell it: <c>.NETCoreApp</c>, <c>.NETFramework</c> or <c>.NETStandard</c>.</param>
/// <param name="Version">The framework version: <c>10.0</c> for <c>net10.0</c>, <c>4.7.2</c> for <c>net472</c>.</param>
/// <param name="Platform">With its version stripped and, for a platform the SDK knows, spelled as the SDK spells it: <c>windows</c>, not <c>Windows7.0</c>.</param>
public sealed record TargetFrameworkMoniker(string Identifier, Version Version, string? Platform)
{
    private const string NetCoreApp = ".NETCoreApp";
    private const string NetFramework = ".NETFramework";
    private const string NetStandard = ".NETStandard";

    /// <summary>
    /// The stamped form names only major and minor: <c>Version=v10.0</c>.
    /// </summary>
    private const int StampedVersionFields = 2;

    /// <summary>
    /// The platforms the SDK names, in its own lower-case spelling. A platform is looked up here
    /// rather than lower-cased, so <c>Windows7.0</c> reads as <c>windows</c>; one not listed keeps
    /// its spelling, which is harmless because every comparison ignores case.
    /// </summary>
    private static readonly FrozenSet<string> KnownPlatforms = new[]
    {
        "android", "browser", "freebsd", "illumos", "ios", "linux", "maccatalyst", "macos", "solaris",
        "tizen", "tvos", "wasi", "windows",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a declared moniker — <c>net10.0</c>, <c>net10.0-windows</c>, <c>netstandard2.0</c>, <c>net472</c>.
    /// </summary>
    internal static TargetFrameworkMoniker? Parse(string declared)
    {
        if (string.IsNullOrWhiteSpace(declared))
        {
            return null;
        }

        var moniker = declared.Trim();
        var dash = moniker.IndexOf('-');
        var platform = dash < 0 ? null : StripVersion(moniker[(dash + 1)..]);

        if (dash >= 0)
        {
            moniker = moniker[..dash];
        }

        if (moniker.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
        {
            return Build(NetStandard, moniker["netstandard".Length..], platform);
        }

        if (moniker.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase))
        {
            return Build(NetCoreApp, moniker["netcoreapp".Length..], platform);
        }

        if (!moniker.StartsWith("net", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>
    /// Reads the attributes an assembly carries.
    /// </summary>
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
            targetPlatform is null ? null : StripVersion(targetPlatform));
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
            ? $"{Identifier},Version=v{Version.ToString(StampedVersionFields)}"
            : $"{Identifier},Version=v{Version.ToString(StampedVersionFields)} ({Platform})";

    private static TargetFrameworkMoniker? Build(string identifier, string version, string? platform)
    {
        var namedPlatform = string.IsNullOrEmpty(platform) ? null : Canonical(platform);

        return Normalise(version) is { } parsed
            ? new TargetFrameworkMoniker(identifier, parsed, namedPlatform)
            : null;
    }

    /// <summary>
    /// <c>10.0</c>, <c>v10.0</c> and <c>10.0.0.0</c> are one version. Without this the
    /// declared moniker and the attribute would never compare equal.
    /// </summary>
    private static Version? Normalise(string version)
    {
        var dotted = version.Contains('.', StringComparison.Ordinal) ? version : version + ".0";

        return Version.TryParse(dotted, out var parsed)
            ? new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0))
            : null;
    }

    /// <summary>
    /// The SDK's spelling of a platform it knows; any other platform as given.
    /// </summary>
    private static string Canonical(string platform) =>
        KnownPlatforms.TryGetValue(platform, out var known) ? known : platform;

    /// <summary>
    /// <c>windows7.0</c> and <c>windows</c> are the same platform.
    /// </summary>
    private static string StripVersion(string platform)
    {
        var index = platform.AsSpan().IndexOfAnyInRange('0', '9');

        return (index < 0 ? platform : platform[..index]).Trim();
    }
}
