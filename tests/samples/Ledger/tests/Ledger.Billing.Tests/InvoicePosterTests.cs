using Ledger.Domain;
using Xunit;

namespace Ledger.Billing.Tests;

public class InvoicePosterTests
{
    private static Invoice InvoiceFor(decimal amount)
    {
        var invoice = new Invoice("Acme", new DateOnly(2026, 3, 1));
        invoice.AddLine(Money.Of(amount));

        return invoice;
    }

    [Fact]
    public void Posting_adds_tax_to_the_total() =>
        Assert.Equal(120m, new InvoicePoster().Post(InvoiceFor(100m)).Gross.Amount);

    [Fact]
    public void Posted_invoices_are_numbered_in_sequence()
    {
        var poster = new InvoicePoster();

        poster.Post(InvoiceFor(1m));

        Assert.Equal("INV-00002", poster.Post(InvoiceFor(1m)).Number);
    }
}
