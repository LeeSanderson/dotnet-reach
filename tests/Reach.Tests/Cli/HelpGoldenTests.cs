using System.CommandLine;
using System.Runtime.CompilerServices;
using Reach.Cli;

namespace Reach.Tests.Cli;

/// <summary>
/// A golden test over <c>--help</c>. Eleven options are <c>--help</c>'s job — there is
/// deliberately no CLI reference document, because a second copy would drift from the parser —
/// which makes the help text the documentation, and an accidental rename a silent breaking
/// change for every pipeline that already spells the option the old way.
/// </summary>
/// <remarks>
/// When the change is intended, run the suite once with <c>REACH_UPDATE_GOLDEN=1</c> and
/// commit the rewritten file.
/// </remarks>
public class HelpGoldenTests
{
    [Fact]
    public void The_select_help_text_has_not_changed_by_accident() =>
        AssertGolden("select-help.txt", HelpFor("select", "--help"));

    [Fact]
    public void The_root_help_text_has_not_changed_by_accident() =>
        AssertGolden("root-help.txt", HelpFor("--help"));

    private static string HelpFor(params string[] arguments)
    {
        var writer = new StringWriter();

        var exitCode = new ReachCommandLine((_, _) => Task.FromResult(0))
            .Parse(arguments)
            .Invoke(new InvocationConfiguration { Output = writer, Error = writer });

        Assert.Equal(0, exitCode);

        // The usage line names the running executable, which under the test host is not
        // 'reach'. Normalised so the golden file describes the shipped tool.
        return writer.ToString()
            .Replace(RootCommand.ExecutableName, "reach", StringComparison.Ordinal)
            .ReplaceLineEndings("\n")
            .TrimEnd();
    }

    private static void AssertGolden(string name, string actual, [CallerFilePath] string testFile = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(testFile)!, "Golden", name);

        if (Environment.GetEnvironmentVariable("REACH_UPDATE_GOLDEN") is { Length: > 0 })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual + "\n");
            return;
        }

        Assert.True(File.Exists(path), $"No golden file at {path}. Run with REACH_UPDATE_GOLDEN=1 to write one.");

        Assert.Equal(
            File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            actual);
    }
}
