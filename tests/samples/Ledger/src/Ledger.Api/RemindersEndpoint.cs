using Ledger.Domain;
using Ledger.Dunning;

namespace Ledger.Api;

/// <summary>
/// Sends reminders for overdue invoices by webhook. Reaches Dunning, and never Billing.
/// </summary>
public sealed class RemindersEndpoint
{
    private readonly ReminderSender _sender = new(new WebhookChannel());

    public IReadOnlyList<string> Request(IEnumerable<Invoice> invoices, DateOnly today) =>
        _sender.SendFor(invoices, today);
}
