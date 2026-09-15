using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Reach.Reporting;

namespace Reach.Tests.Reporting;

/// <summary>
/// <strong>The mechanism that makes M1's whole bargain honest.</strong> Every notice code of
/// kind <c>blind-spot</c> must have an entry in the limitations register, and this asserts the
/// two sets match exactly — so a new blind-spot code fails the build until it is registered.
/// </summary>
/// <remarks>
/// It runs in memory over the catalogue and the document, needs no fixture, and is a test in the
/// suite rather than a CI step: <c>dotnet test</c> runs it and CI needs no knowledge of it.
/// </remarks>
public partial class RegisterParityTests
{
    [Fact]
    public void Every_blind_spot_code_has_a_register_entry()
    {
        var registered = RegisteredCodes();
        var missing = NoticeCatalogue.Of(NoticeKind.BlindSpot).Where(code => !registered.Contains(code));

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_code_the_register_claims_still_exists()
    {
        // The reverse direction, and it matters as much: an entry claiming a code Reach no
        // longer emits is a promise about a gap nobody is watching.
        var orphaned = RegisteredCodes().Where(code => !NoticeCatalogue.Knows(code));

        Assert.Empty(orphaned);
    }

    [Fact]
    public void The_parity_test_fails_when_a_blind_spot_code_is_unregistered()
    {
        // Asserted by construction rather than by trusting the test above: a throwaway code
        // that is not in the register must be reported as missing.
        var registered = RegisteredCodes();
        var pretend = new[] { "ignored-untracked-assembly", "a-hole-nobody-wrote-down" };

        var missing = pretend.Where(code => !registered.Contains(code)).ToArray();

        Assert.Equal(["a-hole-nobody-wrote-down"], missing);
    }

    [Fact]
    public void Every_code_in_the_catalogue_has_a_kind() =>
        Assert.All(NoticeCatalogue.All, entry => Assert.True(Enum.IsDefined(entry.Value)));

    [Fact]
    public void Every_code_is_kebab_case_and_never_numeric()
    {
        // A slug carries most of the explanation to the person staring at a surprising
        // selection, and gives the register a natural anchor per hole instead of a lookup
        // table.
        Assert.All(
            NoticeCatalogue.All.Keys,
            code => Assert.Matches("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", code));
    }

    [Fact]
    public void Every_constant_on_NoticeCodes_is_in_the_catalogue()
    {
        var declared = typeof(NoticeCodes)
            .GetFields(System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(declared);
        Assert.All(declared, code => Assert.True(NoticeCatalogue.Knows(code), code));
    }

    /// <summary>
    /// The codes the register claims to detect, read out of the document. Detectable entries
    /// carry their code in backticks on the line that says so; undetectable ones have none, and
    /// live only in the document — which is the argument for the document existing at all.
    /// </summary>
    private static HashSet<string> RegisteredCodes([CallerFilePath] string testFile = "")
    {
        var path = Path.Combine(RepositoryRoot(testFile), "docs", "limitations.md");

        Assert.True(File.Exists(path), $"No limitations register at {path}.");

        var codes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(path))
        {
            if (!line.Contains("Detectable", StringComparison.Ordinal))
            {
                continue;
            }

            if (!YesDetectable().IsMatch(line))
            {
                continue;
            }

            foreach (Match match in Backticked().Matches(line))
            {
                var code = match.Groups[1].Value;

                // The line also mentions the word "yes" in backticks occasionally, and names
                // kinds. Only things shaped like a code count.
                if (code.Contains('-', StringComparison.Ordinal))
                {
                    codes.Add(code);
                }
            }
        }

        Assert.NotEmpty(codes);

        return codes;
    }

    private static string RepositoryRoot(string testFile)
    {
        var directory = Path.GetDirectoryName(testFile);

        while (directory is not null && !Directory.Exists(Path.Combine(directory, ".git")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);

        return directory;
    }

    /// <summary>
    /// <c>partially</c> counts as detectable: the entry still carries a code, and partial
    /// detection is what most of these holes offer.
    /// </summary>
    [GeneratedRegex(@"\*\*Detectable\*\*:\s*\*{0,2}(yes|partially)", RegexOptions.IgnoreCase)]
    private static partial Regex YesDetectable();

    [GeneratedRegex(@"`([a-z][a-z0-9-]*)`")]
    private static partial Regex Backticked();
}
