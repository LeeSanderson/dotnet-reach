namespace Contoso;

public class Invoice
{
    /// <summary>The rate every consumer inlines, which is what makes it a recompilation trigger.</summary>
    public const int StandardRate = 20;

    public int Total(int net) => net + Tax(net);

    public int Tax(int net) => net * StandardRate / 100;

    public int Untouched() => 0;
}
