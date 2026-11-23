using System.Globalization;
using Ledger.Domain;

namespace Ledger.Dunning;

/// <summary>
/// Sends one reminder for each overdue invoice, through whatever channel it is given.
/// </summary>
/// <remarks>
/// Calls through <see cref="IReminderChannel"/>, a gap-shaped member (known-gap row 26).
/// </remarks>
public sealed class ReminderSender(IReminderChannel channel)
{
    public IReadOnlyList<string> SendFor(IEnumerable<Invoice> invoices, DateOnly today) =>
    [
        .. OverdueInvoices.Of(invoices, today)
            .Select(invoice => channel.Send(invoice.Customer, MessageFor(invoice, today))),
    ];

    private static string MessageFor(Invoice invoice, DateOnly today) =>
        string.Create(CultureInfo.InvariantCulture, $"Your invoice is {invoice.DaysOverdue(today)} days overdue.");
}
