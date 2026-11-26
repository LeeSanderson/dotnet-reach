using Xunit;

namespace Ledger.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Adding_amounts_in_different_currencies_is_refused() =>
        Assert.Throws<InvalidOperationException>(() => new Money(1m, "EUR").Plus(new Money(1m, "USD")));
}
