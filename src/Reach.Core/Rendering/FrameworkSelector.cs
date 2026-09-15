using Reach.Assemblies;

namespace Reach.Rendering;

/// <summary>
/// Turns an assembly's target-framework attributes back into the moniker <c>-f</c> accepts, or
/// says that it cannot.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Every invocation for a project with more than one assembly instance carries
/// <c>-f &lt;moniker&gt;</c>.</strong> <c>dotnet test</c> runs every target framework of a
/// project, so a filter naming a test that exists under only one of them makes the others exit
/// 8 — Reach would render the <em>correct</em> answer and fail the build with it. A
/// single-instance project has nothing to cross-contaminate and gets no selector, which matters
/// more than it sounds: a single-targeted <c>net10.0-windows</c> suite never needs a moniker and
/// never degrades.
/// </para>
/// <para>
/// Derivation is metadata-only. Two rejected alternatives, and neither should be revisited: the
/// output directory name, which <em>is</em> the exact declared moniker under the default layout
/// but reintroduces exactly the path-trust that scan-and-verify spent its whole design removing;
/// and emitting a reconstructed moniker and accepting the risk, which ships a command known to
/// be broken.
/// </para>
/// </remarks>
internal static class FrameworkSelector
{
    /// <summary>
    /// The moniker, or null when it cannot be recovered.
    /// </summary>
    /// <remarks>
    /// <strong>A platform suffix makes it unrecoverable.</strong> <c>TargetPlatformAttribute</c>
    /// always carries a version, so <c>net10.0-windows</c> reads back as <c>Windows7.0</c> and
    /// renders as <c>net10.0-windows7.0</c> — which <c>-f</c> rejects, with an error naming the
    /// wrong subsystem entirely — and a project declaring <c>net10.0-windows7.0</c> produces
    /// byte-identical attributes. No reconstruction rule can be correct for both.
    /// </remarks>
    internal static string? Derive(TargetFrameworkMoniker? framework)
    {
        if (framework is null || framework.Platform is not null)
        {
            return null;
        }

        var version = framework.Version;

        return framework.Identifier switch
        {
            ".NETCoreApp" => $"net{version.Major}.{version.Minor}",
            ".NETStandard" => $"netstandard{version.Major}.{version.Minor}",
            ".NETFramework" => "net" + Digits(version),
            _ => null,
        };
    }

    /// <summary><c>4.7.2</c> becomes <c>472</c>, and <c>4.8.0</c> becomes <c>48</c>.</summary>
    private static string Digits(Version version)
    {
        var parts = version.Build > 0
            ? new[] { version.Major, version.Minor, version.Build }
            : [version.Major, version.Minor];

        return string.Concat(parts);
    }
}
