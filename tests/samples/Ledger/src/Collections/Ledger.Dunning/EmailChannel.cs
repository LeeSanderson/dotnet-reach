using Ledger.Domain;

namespace Ledger.Dunning;

/// <summary>
/// Sends reminders by email. Gap-shaped: known-gap row 26 changes it.
/// </summary>
public sealed class EmailChannel : IReminderChannel
{
    public string Send(string customer, string message) => $"email to {customer}: {message}";
}
