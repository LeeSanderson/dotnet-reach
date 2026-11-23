using Xunit;

namespace Ledger.Dunning.Tests;

public class ReminderScheduleTests
{
    [Fact]
    public void Each_reminder_waits_a_week_longer_than_the_last() =>
        Assert.Equal(new DateOnly(2026, 3, 15), ReminderSchedule.Next(new DateOnly(2026, 3, 1), remindersSent: 1));
}
