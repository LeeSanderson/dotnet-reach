namespace Contoso.Tests.Unrecognised;

public class InvoiceChecks
{
    [ContosoCheck]
    public void Totals_include_tax()
    {
        if (new Invoice().Total(100) != 120)
        {
            throw new InvalidOperationException();
        }
    }
}
