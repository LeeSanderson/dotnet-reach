using Ledger.Domain;

namespace Ledger.Dunning;

/// <summary>
/// Picks out the invoices past their due date.
/// </summary>
public static class OverdueInvoices
{
    public static IReadOnlyList<Invoice> Of(IEnumerable<Invoice> invoices, DateOnly today) =>
        [.. invoices.Where(invoice => invoice.DaysOverdue(today) > 0)];
}
