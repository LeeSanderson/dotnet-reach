namespace Contoso.Tests.Unrecognised;

/// <summary>A test framework Reach has never heard of, declared inline so it needs no package.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ContosoCheckAttribute : Attribute;

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
