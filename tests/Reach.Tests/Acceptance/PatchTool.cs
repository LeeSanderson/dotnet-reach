namespace Reach.Tests.Acceptance;

/// <summary>
/// The commands of <c>tests/tools/patch.cs</c>, the one way a patch is written, as a failure
/// message tells a contributor to type them.
/// </summary>
internal static class PatchTool
{
    private const string Command = "dotnet run tests/tools/patch.cs --";

    /// <summary>
    /// Applies a sample's patch to the sample in the working tree, on its baseline patch when
    /// <paramref name="on"/> names one.
    /// </summary>
    internal static string Apply(string sample, string name, string? on = null) =>
        $"{Command} apply {sample} {name}{On(on)}";

    /// <summary>
    /// Saves the working tree's changes to the sample over an existing patch.
    /// </summary>
    internal static string Save(string sample, string name, string? on = null) =>
        $"{Command} save {sample} {name}{On(on)} --force";

    private static string On(string? baseline) => baseline is null ? string.Empty : $" --on {baseline}";
}
