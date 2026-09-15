namespace Reach.Cli;

internal static class Program
{
    internal static Task<int> Main(string[] args) =>
        ReachCli.RunAsync(
            args,
            Environment.CurrentDirectory,
            Console.Out,
            Console.Error,
            Environment.GetEnvironmentVariable,
            Console.IsOutputRedirected);
}
