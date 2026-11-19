using Reach.Rendering;

namespace Reach.Tests.Rendering;

/// <summary>
/// A selection too long for one command line on whichever platform the tests run on.
/// </summary>
/// <remarks>
/// Sized from the platform's own ceiling rather than a fixed count, so the chunking and
/// response-file paths are exercised on Linux (100,000) as well as on Windows (8,000).
/// </remarks>
internal static class LongSelection
{
    private const string Prefix = "Contoso.Billing.Tests.InvoiceTests.Calculates_the_total_case_";

    private const int IndexDigits = 5;

    /// <summary>
    /// Names whose bare text alone exceeds the ceiling, before any filter syntax is added.
    /// </summary>
    internal static IReadOnlyList<string> OverflowingTheCeiling()
    {
        var count = (CommandLineCeiling.Characters / (Prefix.Length + IndexDigits)) + 100;

        return [.. Enumerable.Range(0, count).Select(index => $"{Prefix}{index:D5}")];
    }
}
