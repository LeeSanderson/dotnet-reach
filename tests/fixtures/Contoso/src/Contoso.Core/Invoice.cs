namespace Contoso;

public static class Invoice
{
    /// <summary>
    /// The rate every consumer inlines, which is what makes it a recompilation trigger.
    /// </summary>
    public const int StandardRate = 20;

    private const int Percent = 100;

    public static int Total(int net) => net + Tax(net);

    public static int Tax(int net) => net * StandardRate / Percent;

    public static int Untouched(int net) => net;
}
