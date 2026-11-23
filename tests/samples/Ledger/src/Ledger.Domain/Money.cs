namespace Ledger.Domain;

/// <summary>
/// An amount in one currency.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    /// <summary>
    /// Gap-shaped: known-gap row 27 changes it. Reach widens every project that references the
    /// one declaring a changed <c>const</c>, whatever its visibility, so a change selects every
    /// test. No gate patch touches it.
    /// </summary>
    /// <remarks>
    /// Internal, not public: a public constant trips Sonar S2339, for the very inlining Reach
    /// widens against.
    /// </remarks>
    internal const string DefaultCurrency = "GBP";

    public static Money Of(decimal amount) => new(amount, DefaultCurrency);

    public Money Plus(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot add {other.Currency} to {Currency}.");
        }

        return this with { Amount = Amount + other.Amount };
    }
}
