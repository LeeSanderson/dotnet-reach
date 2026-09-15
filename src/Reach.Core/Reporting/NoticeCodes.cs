namespace Reach.Reporting;

/// <summary>
/// Every notice code Reach emits. Kebab-case and never renamed: a consumer gating on one is
/// depending on the string, and the limitations register anchors on it.
/// </summary>
/// <remarks>
/// The catalogue that binds each code to its kind, and the parity test that fails the build
/// when a <see cref="NoticeKind.BlindSpot"/> code has no register entry, arrive with the
/// notice catalogue. Codes are added here as the phase that emits them lands.
/// </remarks>
internal static class NoticeCodes
{
    /// <summary>How the baseline was arrived at, and what SHA it resolved to.</summary>
    internal const string BaselineResolved = "baseline-resolved";

    /// <summary>The baseline resolved to <c>HEAD</c>, so the change set can only be the working tree.</summary>
    internal const string BaselineIsHead = "baseline-is-head";

    /// <summary>No baseline could be resolved. The run failed; the message says how to fix it.</summary>
    internal const string BaselineUnresolvable = "baseline-unresolvable";

    /// <summary>
    /// Reach was pointed at a project that no solution above it lists, so there is no expected
    /// project list to check the build output against.
    /// </summary>
    internal const string ProjectOutsideEverySolution = "project-outside-every-solution";
}
