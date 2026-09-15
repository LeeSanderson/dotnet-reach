namespace Reach;

/// <summary>
/// What the process exits with. Non-zero means <em>do not trust my answer</em>, which is what
/// makes the pipeline rule operational: if Reach exits non-zero, run the whole suite or stop
/// the build.
/// </summary>
internal enum ExitCode
{
    /// <summary>Success, including an empty selection.</summary>
    Success = 0,

    /// <summary>Usage error. The one case that writes no report.</summary>
    UsageError = 1,

    /// <summary>The build failed.</summary>
    BuildFailed = 2,

    /// <summary>An assembly was missing, or assembly discovery failed.</summary>
    AssemblyDiscoveryFailed = 3,

    /// <summary>The baseline could not be resolved, including a shallow clone.</summary>
    BaselineUnresolvable = 4,

    /// <summary>Source and binary do not correspond, so the graph would describe code that is not there.</summary>
    CorrespondenceFailed = 5,

    /// <summary>An internal error. A bug in Reach, not in the solution it was pointed at.</summary>
    InternalError = 70,
}
