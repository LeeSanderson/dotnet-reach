using Ledger.Domain;

namespace Ledger.Billing;

/// <summary>
/// An invoice once it is posted: numbered, and with tax added.
/// </summary>
public sealed record PostedInvoice(string Number, string Customer, Money Gross);
