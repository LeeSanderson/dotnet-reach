namespace Contoso.Tests.Unrecognised;

public class InvoiceChecks
{
    private const int Net = 100;
    private const int TotalWithTax = 120;

    [ContosoCheck]
    public void Totals_include_tax()
    {
        if (Invoice.Total(Net) != TotalWithTax)
        {
            throw new InvalidOperationException();
        }
    }
}
