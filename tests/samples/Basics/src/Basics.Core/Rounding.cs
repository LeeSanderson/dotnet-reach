namespace Basics.Core;

/// <summary>
/// Row 3's subject: a check of it in <c>RoundingChecks</c> becomes a test. Only row 3's patch
/// touches its tests.
/// </summary>
public static class Rounding
{
    private const int CentDigits = 2;

    public static decimal ToCents(decimal amount) => Math.Round(amount, CentDigits, MidpointRounding.ToEven);
}
