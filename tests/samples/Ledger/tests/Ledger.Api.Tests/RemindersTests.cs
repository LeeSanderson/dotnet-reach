using Ledger.Domain;
using Xunit;

namespace Ledger.Api.Tests;

public class RemindersTests
{
    [Fact]
    public void Requesting_reminders_lists_the_overdue_invoices()
    {
        var late = new Invoice("Late Ltd", new DateOnly(2026, 3, 1));
        var early = new Invoice("Early Ltd", new DateOnly(2026, 4, 1));

        var sent = new RemindersEndpoint().Request([late, early], new DateOnly(2026, 3, 4));

        Assert.Equal(["POST /hooks/reminders Late Ltd: Your invoice is 3 days overdue."], sent);
    }
}
