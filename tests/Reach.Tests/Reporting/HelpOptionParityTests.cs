using System.Text.RegularExpressions;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Reporting;

/// <summary>
/// The option names in <c>select --help</c> are the options <c>docs/adopting-reach.md</c> and
/// <c>docs/report-schema.md</c> describe: nothing the docs name is missing from the parser, and
/// nothing the parser takes goes undescribed.
/// </summary>
/// <remarks>
/// <para>
/// Names only. The descriptions stay hand-kept. The help text is read from the golden file,
/// which <c>HelpGoldenTests</c> holds equal to the parser.
/// </para>
/// <para>
/// The second direction is what proves that notices cannot be suppressed: a switch that
/// suppressed them would be an option the docs do not describe.
/// </para>
/// </remarks>
public sealed partial class HelpOptionParityTests
{
    /// <summary>
    /// Options of other tools the docs name, each beside a source link: cited facts, outside the
    /// gate. Anything else in backticks that looks like an option is taken to be Reach's.
    /// </summary>
    private static readonly HashSet<string> OtherToolsOptions = new(StringComparer.Ordinal)
    {
        "-g",                   // dotnet tool install
        "--prerelease",         // dotnet tool exec
        "--project",            // dotnet test
        "--ignore-exit-code",   // Microsoft.Testing.Platform
        "--zero-tests-policy",  // Microsoft.Testing.Platform
    };

    private static readonly string[] Documents =
    [
        Path.Combine("docs", "adopting-reach.md"),
        Path.Combine("docs", "report-schema.md"),
    ];

    [Fact]
    public void The_docs_describe_exactly_the_options_select_help_lists() =>
        Assert.Empty(Drift(HelpOptions(), Documented()));

    [Fact]
    public void An_option_the_docs_do_not_describe_fails_the_comparison()
    {
        // Asserted by construction rather than by trusting the test above, with the switch the
        // schema says will never ship.
        var drift = Drift([.. HelpOptions(), new HelpOption(["--suppress-notices"])], Documented());

        Assert.Equal(["`--suppress-notices` is in select --help, but no doc describes it."], drift);
    }

    [Fact]
    public void An_option_the_parser_does_not_take_fails_the_comparison()
    {
        // The option that was cut while it was still described.
        var documented = new HashSet<string>(Documented(), StringComparer.Ordinal) { "--list-unselected" };

        var drift = Drift(HelpOptions(), documented);

        Assert.Equal(["`--list-unselected` is described in the docs, but select --help has no such option."], drift);
    }

    private static List<string> Drift(IReadOnlyList<HelpOption> help, IReadOnlySet<string> documented)
    {
        var known = help.SelectMany(option => option.Names).ToHashSet(StringComparer.Ordinal);

        var phantom = documented
            .Where(option => !known.Contains(option) && !OtherToolsOptions.Contains(option))
            .Order(StringComparer.Ordinal)
            .Select(option => $"`{option}` is described in the docs, but select --help has no such option.");

        var undescribed = help
            .Where(option => !option.Names.Any(documented.Contains))
            .Select(option => $"`{option.Names[^1]}` is in select --help, but no doc describes it.");

        return [.. phantom, .. undescribed];
    }

    /// <summary>
    /// Every name and alias of each option under <c>Options:</c>, one list per option.
    /// </summary>
    private static List<HelpOption> HelpOptions()
    {
        var options = RepositoryRoot.ReadText("tests", "Reach.Tests", "Cli", "Golden", "select-help.txt").Split('\n')
            .SkipWhile(line => !string.Equals(line, "Options:", StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(line => line.StartsWith("  ", StringComparison.Ordinal))
            .Select(line => line.TrimStart().Split("  ")[0])
            .Select(names => new HelpOption([.. HelpName.Matches(names).Select(name => name.Value)]))
            .ToList();

        Assert.NotEmpty(options);

        return options;
    }

    [GeneratedRegex(@"-{1,2}(?:\?|[a-z][a-z0-9-]*)")]
    private static partial Regex HelpName { get; }

    /// <summary>
    /// Every option-shaped token inside backticks in the prose of the documents. Fenced blocks
    /// are skipped: they are workflows that run other tools, and a recipe is not a description.
    /// </summary>
    private static HashSet<string> Documented()
    {
        var options = new HashSet<string>(StringComparer.Ordinal);

        foreach (var document in Documents)
        {
            var fenced = false;

            foreach (var line in RepositoryRoot.ReadText(document).Split('\n'))
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    fenced = !fenced;
                    continue;
                }

                if (fenced)
                {
                    continue;
                }

                foreach (Match span in Backticked.Matches(line))
                {
                    options.UnionWith(DocumentedName.Matches(span.Groups[1].Value).Select(name => name.Value));
                }
            }
        }

        Assert.NotEmpty(options);

        return options;
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex Backticked { get; }

    /// <summary>
    /// A leading dash not preceded by a word character, so <c>dotnet-reach</c> and
    /// <c>merge-base</c> are not options, and a letter after it, so the bare <c>--</c> boundary
    /// is not either.
    /// </summary>
    [GeneratedRegex(@"(?<![\w-])-{1,2}[a-z][a-z0-9-]*")]
    private static partial Regex DocumentedName { get; }

    /// <summary>
    /// One option in the help text: its aliases, then its long name last.
    /// </summary>
    private sealed record HelpOption(IReadOnlyList<string> Names);
}
