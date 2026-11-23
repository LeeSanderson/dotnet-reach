using Frameworks.Core;

namespace Frameworks.Tests.MSTest;

[TestClass]
public sealed class InvoiceTests
{
    [TestMethod]
    public void Totals_include_tax() => Assert.AreEqual(120, Invoice.Total(100));

    [TestMethod]
    public void Untouched_is_zero() => Assert.AreEqual(0, Invoice.Untouched(0));
}
