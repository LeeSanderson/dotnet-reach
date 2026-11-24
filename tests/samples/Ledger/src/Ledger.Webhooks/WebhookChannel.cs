using Ledger.Domain;

namespace Ledger.Webhooks;

/// <summary>
/// Sends reminders to a webhook. Gap-shaped (known-gap row 26): a change to
/// <c>EmailChannel</c> still selects the test that uses this channel.
/// </summary>
/// <remarks>
/// It lives in a project of its own, not in Api, so that no <c>IReminderChannel</c> implementer
/// sits in an assembly a gate row widens whole. Rows 18 and 19 widen Api whole; were this channel
/// there, the reverse walk from its <c>Send</c> would come back into Dunning through
/// <c>ReminderSender</c>'s interface call.
/// </remarks>
public sealed class WebhookChannel : IReminderChannel
{
    public string Send(string customer, string message) => $"POST /hooks/reminders {customer}: {message}";
}
