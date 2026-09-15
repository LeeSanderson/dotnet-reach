namespace Reach.Cli;

internal static class Program
{
    internal static int Main(string[] args)
    {
        // The command line arrives in ticket 05. Until then the entry point exists only so
        // that the solution builds, packs and runs.
        Console.Error.WriteLine($"reach {Product.Version}: no verb is implemented yet.");
        return 2;
    }
}
