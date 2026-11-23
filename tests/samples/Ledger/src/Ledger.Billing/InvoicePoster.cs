using Ledger.Domain;

namespace Ledger.Billing;

/// <summary>
/// Posts invoices: numbers each one and adds its tax.
/// </summary>
public sealed class InvoicePoster
{
    private readonly InvoiceNumbering _numbering = new();

    public PostedInvoice Post(Invoice invoice)
    {
        var net = invoice.Total();

        return new PostedInvoice(_numbering.Next(), invoice.Customer, net.Plus(TaxRules.TaxOn(net)));
    }
}
