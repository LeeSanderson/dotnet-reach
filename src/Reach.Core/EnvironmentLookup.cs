namespace Reach;

/// <summary>
/// Reads one environment variable. Not a port — there is nothing here to fake but a lookup,
/// and an interface would be larger than what it wraps.
/// </summary>
/// <remarks>
/// Every read of the environment goes through one of these, so a test can drive the five CI
/// detection variables, <c>NO_COLOR</c> and a provider marker without setting any of them on
/// the machine running the suite.
/// </remarks>
internal delegate string? EnvironmentLookup(string name);
