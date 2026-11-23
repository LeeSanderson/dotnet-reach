using Ledger.Domain;
using Xunit;

namespace Ledger.Dunning.Tests;

public class ReminderSenderTests
{
    [Fact]
    public void A_reminder_names_the_days_overdue()
    {
        var invoice = new Invoice("Acme", new DateOnly(2026, 3, 1));

        var sent = new ReminderSender(new EmailChannel()).SendFor([invoice], new DateOnly(2026, 3, 11));

        Assert.Equal(["email to Acme: Your invoice is 10 days overdue."], sent);
    }
}
