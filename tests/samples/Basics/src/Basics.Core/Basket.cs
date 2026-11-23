namespace Basics.Core;

/// <summary>
/// Calls <see cref="Discount.Apply"/>, so a test of the basket reaches the discount through a
/// second compiled edge.
/// </summary>
public sealed class Basket(int discountPercent)
{
    private readonly List<decimal> _prices = [];

    public void Add(decimal price) => _prices.Add(price);

    public decimal Total() => Discount.Apply(_prices.Sum(), discountPercent);
}
