using Ledger.Domain;

namespace Ledger.Api;

/// <summary>
/// Sends reminders to a webhook. Gap-shaped (known-gap row 26): a change to
/// <c>EmailChannel</c> still selects the test that uses this channel.
/// </summary>
public sealed class WebhookChannel : IReminderChannel
{
    public string Send(string customer, string message) => $"POST /hooks/reminders {customer}: {message}";
}
