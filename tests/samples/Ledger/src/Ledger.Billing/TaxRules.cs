using Ledger.Domain;

namespace Ledger.Billing;

/// <summary>
/// The tax added to an invoice when it is posted.
/// </summary>
public static class TaxRules
{
    /// <summary>
    /// Row 18 changes it: a <c>const</c> can be inlined into consumers, so every project that
    /// references Billing widens. Internal for the same reason as <c>Money.DefaultCurrency</c>.
    /// </summary>
    internal const int StandardRatePercent = 20;

    private const int WholePercent = 100;

    public static Money TaxOn(Money net) => net with { Amount = net.Amount * StandardRatePercent / WholePercent };
}
