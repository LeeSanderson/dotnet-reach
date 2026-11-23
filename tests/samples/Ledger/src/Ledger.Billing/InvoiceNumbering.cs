using System.Globalization;

namespace Ledger.Billing;

/// <summary>
/// Gives each posted invoice the next number in sequence. Row 22 moves it to another namespace.
/// </summary>
public sealed class InvoiceNumbering
{
    private int _last;

    public string Next()
    {
        _last++;

        return string.Create(CultureInfo.InvariantCulture, $"INV-{_last:D5}");
    }
}
