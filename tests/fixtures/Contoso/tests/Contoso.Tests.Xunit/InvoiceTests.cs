using Xunit;

namespace Contoso.Tests;

public class InvoiceTests
{
    [Fact]
    public void Totals_include_tax() => Assert.Equal(120, Invoice.Total(100));

    [Fact]
    public void Untouched_is_zero() => Assert.Equal(0, Invoice.Untouched(0));
}
