namespace Reach;

/// <summary>
/// Mirrors the SDK's <c>quiet|minimal|normal|detailed|diagnostic</c>, spelling included, so a
/// pipeline author transfers what they already know.
/// </summary>
public enum Verbosity
{
    Quiet,
    Minimal,
    Normal,
    Detailed,
    Diagnostic,
}
