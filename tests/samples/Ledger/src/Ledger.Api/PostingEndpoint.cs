using System.Globalization;
using Ledger.Billing;
using Ledger.Domain;

namespace Ledger.Api;

/// <summary>
/// Posts an invoice. Reaches Billing, and never Dunning.
/// </summary>
public sealed class PostingEndpoint
{
    private readonly InvoicePoster _poster = new();

    public string Post(Invoice invoice)
    {
        var posted = _poster.Post(invoice);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{posted.Number}: {posted.Gross.Amount:0.00} {posted.Gross.Currency}");
    }
}
