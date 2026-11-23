using Ledger.Domain;
using Xunit;

namespace Ledger.Api.Tests;

public class PostingTests
{
    [Fact]
    public void Posting_an_invoice_returns_its_total()
    {
        var invoice = new Invoice("Acme", new DateOnly(2026, 3, 1));
        invoice.AddLine(Money.Of(100m));

        Assert.Equal("INV-00001: 120.00 GBP", new PostingEndpoint().Post(invoice));
    }
}
