namespace Frameworks.Core;

/// <summary>
/// The production code every test project calls, whatever its framework.
/// </summary>
public static class Invoice
{
    private const int StandardRate = 20;

    private const int Percent = 100;

    public static int Total(int net) => net + Tax(net);

    public static int Tax(int net) => net * StandardRate / Percent;

    public static int Untouched(int net) => net;
}
