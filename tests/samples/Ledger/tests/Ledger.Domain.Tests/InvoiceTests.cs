using Xunit;

namespace Ledger.Domain.Tests;

public class InvoiceTests
{
    private static readonly DateOnly Due = new(2026, 3, 1);

    [Fact]
    public void The_total_sums_the_lines()
    {
        var invoice = new Invoice("Acme", Due);

        invoice.AddLine(Money.Of(30m));
        invoice.AddLine(Money.Of(12.5m));

        Assert.Equal(42.5m, invoice.Total().Amount);
    }

    [Fact]
    public void Overdue_counts_calendar_days() =>
        Assert.Equal(10, new Invoice("Acme", Due).DaysOverdue(new DateOnly(2026, 3, 11)));
}
