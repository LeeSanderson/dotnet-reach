using Ledger.Domain;
using Xunit;

namespace Ledger.Billing.Tests;

public class TaxRulesTests
{
    [Fact]
    public void Tax_is_the_standard_rate_of_the_net() =>
        Assert.Equal(10m, TaxRules.TaxOn(new Money(50m, "GBP")).Amount);
}
