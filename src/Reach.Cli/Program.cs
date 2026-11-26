using Reach.Processes;

namespace Reach.Cli;

internal static class Program
{
    internal static Task<int> Main(string[] args) =>
        new ReachCli(new ProcessRunner()).RunAsync(
            args,
            Environment.CurrentDirectory,
            Console.Out,
            Console.Error,
            Environment.GetEnvironmentVariable,
            Console.IsOutputRedirected);
}
