namespace Basics.Core;

/// <summary>
/// Row 4: the other implementation of <see cref="IShippingRate"/>.
/// </summary>
public sealed class WeightRate : IShippingRate
{
    private const decimal PerKg = 2m;

    public decimal Cost(decimal weightKg) => weightKg * PerKg;
}
