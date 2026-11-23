using Ledger.Domain;
using Xunit;

namespace Ledger.Dunning.Tests;

public class OverdueInvoicesTests
{
    [Fact]
    public void Only_overdue_invoices_are_listed()
    {
        var late = new Invoice("Late Ltd", new DateOnly(2026, 3, 1));
        var early = new Invoice("Early Ltd", new DateOnly(2026, 4, 1));

        Assert.Equal([late], OverdueInvoices.Of([late, early], new DateOnly(2026, 3, 11)));
    }
}
