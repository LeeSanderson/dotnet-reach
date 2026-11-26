using Frameworks.Core;

namespace Frameworks.Tests.Unrecognised;

public class InvoiceChecks
{
    private const int Net = 100;
    private const int TotalWithTax = 120;

    [FrameworksCheck]
    public void Totals_include_tax()
    {
        if (Invoice.Total(Net) != TotalWithTax)
        {
            throw new InvalidOperationException();
        }
    }

    [FrameworksCheck]
    public void Untouched_is_zero()
    {
        if (Invoice.Untouched(0) != 0)
        {
            throw new InvalidOperationException();
        }
    }
}
