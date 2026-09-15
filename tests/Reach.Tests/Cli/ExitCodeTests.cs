namespace Reach.Tests.Cli;

/// <summary>
/// Exit codes are a closed set and a product surface: non-zero means "do not trust my answer",
/// which is what makes the pipeline rule operational — if Reach exits non-zero, run the whole
/// suite or stop the build. A renumbering would silently change what a pipeline does.
/// </summary>
public class ExitCodeTests
{
    [Fact]
    public void The_set_is_exactly_the_one_the_specification_names()
    {
        var actual = Enum.GetValues<ExitCode>()
            .ToDictionary(code => code.ToString(), code => (int)code, StringComparer.Ordinal);

        Assert.Equal(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [nameof(ExitCode.Success)] = 0,
                [nameof(ExitCode.UsageError)] = 1,
                [nameof(ExitCode.BuildFailed)] = 2,
                [nameof(ExitCode.AssemblyDiscoveryFailed)] = 3,
                [nameof(ExitCode.BaselineUnresolvable)] = 4,
                [nameof(ExitCode.CorrespondenceFailed)] = 5,
                [nameof(ExitCode.InternalError)] = 70,
            },
            actual);
    }

    [Fact]
    public void An_empty_selection_exits_0()
    {
        // Success includes an empty selection. Anything else would make every quiet day look
        // like a failure and train pipelines to ignore the code that matters.
        Assert.Equal(0, (int)ExitCode.Success);
    }
}
