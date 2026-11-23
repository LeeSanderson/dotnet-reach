namespace Ledger.Dunning;

/// <summary>
/// When the next reminder is due. Each one waits a week longer than the last.
/// </summary>
public static class ReminderSchedule
{
    private const int DaysPerWeek = 7;

    /// <summary>
    /// Unused on purpose: row 19 removes it, and a removed member widens every consumer.
    /// </summary>
    public static DateOnly Next(DateOnly lastSent) => Next(lastSent, remindersSent: 0);

    public static DateOnly Next(DateOnly lastSent, int remindersSent) =>
        lastSent.AddDays(DaysPerWeek * (remindersSent + 1));
}
