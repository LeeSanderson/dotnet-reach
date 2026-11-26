namespace Basics.Core;

/// <summary>
/// Row 4: calls <see cref="IShippingRate.Cost"/> through the interface, so a change to either
/// implementation reaches every test that quotes through it.
/// </summary>
public static class Shipping
{
    public static decimal Quote(IShippingRate rate, decimal weightKg) => rate.Cost(weightKg);
}
