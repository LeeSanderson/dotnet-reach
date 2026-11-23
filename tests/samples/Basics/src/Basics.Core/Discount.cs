namespace Basics.Core;

/// <summary>
/// Row 1's subject: a percentage off a price. Only row 1's patch touches it.
/// </summary>
public static class Discount
{
    private const int WholePercent = 100;

    public static decimal Apply(decimal price, int percent) => price - (price * percent / WholePercent);

    public static bool IsValid(int percent) => percent is >= 0 and <= WholePercent;
}
