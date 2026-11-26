namespace Ledger.Domain;

/// <summary>
/// Somewhere a reminder can be sent.
/// </summary>
/// <remarks>
/// Gap-shaped: known-gap row 26 changes one implementer, and every caller through this interface
/// is selected, whichever implementer it really uses. No gate patch touches it or its
/// implementers.
/// </remarks>
public interface IReminderChannel
{
    /// <summary>
    /// Sends <paramref name="message"/> to <paramref name="customer"/> and says what was sent.
    /// </summary>
    string Send(string customer, string message);
}
