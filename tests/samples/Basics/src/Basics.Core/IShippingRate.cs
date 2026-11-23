namespace Basics.Core;

/// <summary>
/// Row 4's interface, with two implementations and a caller, <see cref="Shipping"/>, that sees
/// only the interface. Only row 4's patch touches this hierarchy.
/// </summary>
public interface IShippingRate
{
    decimal Cost(decimal weightKg);
}
