namespace Ledger.Domain;

/// <summary>
/// What a customer owes, and by when.
/// </summary>
public sealed class Invoice(string customer, DateOnly due)
{
    private readonly List<Money> _lines = [];

    public string Customer => customer;

    public void AddLine(Money amount) => _lines.Add(amount);

    public Money Total() => _lines.Aggregate(Money.Of(0m), (total, line) => total.Plus(line));

    /// <summary>
    /// Only Dunning calls this, so a change to it leaves Billing's tests out (row 17).
    /// </summary>
    public int DaysOverdue(DateOnly today) => today > due ? today.DayNumber - due.DayNumber : 0;
}
