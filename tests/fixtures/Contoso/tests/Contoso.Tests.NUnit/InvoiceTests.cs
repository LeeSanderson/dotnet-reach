using NUnit.Framework;

namespace Contoso.Tests.Nunit;

public class InvoiceTests
{
    // A parameterised test: NUnit's VSTest FullyQualifiedName includes the arguments, which is
    // the whole reason the rendered filter has to match by containment rather than equality.
    [TestCase(100, 120)]
    [TestCase(200, 240)]
    public void MyTest(int net, int expected) => Assert.That(new Invoice().Total(net), Is.EqualTo(expected));

    // Deliberately reaches something else, so a change to Tax selects MyTest and not this — and
    // the rendered `~MyTest` filter then matches it anyway. That gap is the second number.
    [Test]
    public void MyTest2() => Assert.That(new Invoice().Untouched(), Is.EqualTo(0));

    [Test]
    public void Unrelated() => Assert.Pass();
}
