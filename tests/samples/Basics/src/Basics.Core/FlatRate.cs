namespace Basics.Core;

/// <summary>
/// Row 4: one implementation of <see cref="IShippingRate"/>.
/// </summary>
public sealed class FlatRate : IShippingRate
{
    private const decimal Fee = 5m;

    public decimal Cost(decimal weightKg) => Fee;
}
