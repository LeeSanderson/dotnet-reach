using Reach.Assemblies;
using Reach.Rendering;

namespace Reach.Tests.Rendering;

/// <summary>
/// The filter grammar, the ceiling and the framework selector — the three things a wrong
/// rendering turns into a failing build or a silently missing test.
/// </summary>
public class RenderingTests
{
    // ---- Rule 1: NUnit renders with contains ---------------------------------------------------

    [Theory]
    [InlineData("xunit-v2", "=")]
    [InlineData("xunit-v3", "=")]
    [InlineData("xunit-v3-4", "=")]
    [InlineData("mstest", "=")]
    [InlineData("nunit", "~")]
    public void Each_dialect_renders_with_its_own_operator(string dialect, string expected) =>
        Assert.Equal(
            "FullyQualifiedName" + expected + "Ns.C.M",
            FilterDialect.Clause(dialect, "Ns.C.M"));

    [Fact]
    public void NUnit_renders_contains_and_never_equality()
    {
        // Asserted directly, because equality is what anyone would write first. NUnit's VSTest
        // FullyQualifiedName includes a parameterised test's arguments, so equality matches
        // nothing except through an adapter re-parse that three ordinary configurations
        // disable — and Reach can see none of them.
        var clause = FilterDialect.Clause("nunit", "Ns.C.MyTest");

        Assert.Contains("~", clause);
        Assert.DoesNotContain("=", clause);
    }

    [Fact]
    public void An_expression_joins_clauses_with_or() =>
        Assert.Equal(
            "FullyQualifiedName=A|FullyQualifiedName=B",
            FilterDialect.Expression("xunit-v3-4", ["A", "B"]));

    // ---- Escaping ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("Ns.C.M", "Ns.C.M")]
    [InlineData("Ns.C+Nested.M", "Ns.C+Nested.M")]
    [InlineData("Ns.C`1.M", "Ns.C`1.M")]
    [InlineData("Ns.C.M(1,2)", @"Ns.C.M\(1\,2\)")]
    [InlineData("Ns.C.M=x", @"Ns.C.M\=x")]
    [InlineData("Ns.C.M&y", @"Ns.C.M\&y")]
    [InlineData(@"Ns.C.M\z", @"Ns.C.M\\z")]
    [InlineData("Ns.C.M~w", @"Ns.C.M\~w")]
    [InlineData("Ns.C.M!v", @"Ns.C.M\!v")]
    [InlineData("Ns.C.M|u", @"Ns.C.M\|u")]
    public void Names_carrying_grammar_characters_are_escaped(string name, string expected) =>
        Assert.Equal(expected, FilterDialect.Escape(name));

    [Fact]
    public void A_name_with_every_special_character_still_renders_one_clause()
    {
        var clause = FilterDialect.Clause("xunit-v3-4", @"Ns.C+N`1.M(a,b)=c&d|e!f~g\h");

        // One clause: nothing in the escaped name can be read as a second one, because every
        // grammar character in it is preceded by a backslash.
        Assert.StartsWith("FullyQualifiedName=", clause);
        Assert.Equal(0, UnescapedCount(clause["FullyQualifiedName=".Length..], '|'));
        Assert.Equal(0, UnescapedCount(clause["FullyQualifiedName=".Length..], '&'));
    }

    private static int UnescapedCount(string text, char character)
    {
        var count = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
            }
            else if (text[index] == character)
            {
                count++;
            }
        }

        return count;
    }

    // ---- Rendered match counts ---------------------------------------------------------------------

    [Fact]
    public void Contains_matching_reports_MyTest2_as_an_extra()
    {
        var matches = FilterDialect.Matches(
            "nunit",
            ["Ns.C.MyTest"],
            ["Ns.C.MyTest", "Ns.C.MyTest2", "Ns.C.Other"]);

        // The headline over-selection metric reads this number, so a rendering that let it
        // silently return the selection's own size would corrupt the measurement.
        Assert.Equal(["Ns.C.MyTest", "Ns.C.MyTest2"], matches);
    }

    [Fact]
    public void Equality_matching_reports_no_extras() =>
        Assert.Equal(
            ["Ns.C.MyTest"],
            FilterDialect.Matches("xunit-v3-4", ["Ns.C.MyTest"], ["Ns.C.MyTest", "Ns.C.MyTest2"]));

    // ---- The chunker -------------------------------------------------------------------------------

    [Fact]
    public void The_chunker_partitions_the_set_with_nothing_dropped_or_duplicated()
    {
        var names = Enumerable.Range(0, 200)
            .Select(index => $"Contoso.Billing.Tests.InvoiceTests.Calculates_the_total_case_{index:D3}")
            .ToArray();

        var chunks = Chunker.Split("xunit-v3-4", names, overhead: 200);

        Assert.True(chunks.Count > 1, "200 tests should not fit on one command line");

        var flattened = chunks.SelectMany(chunk => chunk).ToArray();

        Assert.Equal(names.Length, flattened.Length);
        Assert.Equal(names.Order(StringComparer.Ordinal), flattened.Order(StringComparer.Ordinal));
        Assert.All(chunks, chunk => Assert.NotEmpty(chunk));
    }

    [Fact]
    public void Every_chunk_fits_under_the_ceiling()
    {
        var names = Enumerable.Range(0, 200)
            .Select(index => $"Contoso.Billing.Tests.InvoiceTests.Calculates_the_total_case_{index:D3}")
            .ToArray();

        foreach (var chunk in Chunker.Split("xunit-v3-4", names, overhead: 200))
        {
            Assert.True(
                FilterDialect.Expression("xunit-v3-4", chunk).Length + 200 <= CommandLineCeiling.Characters,
                "a chunk exceeded the ceiling");
        }
    }

    [Fact]
    public void A_short_selection_is_one_chunk() =>
        Assert.Single(Chunker.Split("xunit-v3-4", ["Ns.C.A", "Ns.C.B"], overhead: 200));

    [Fact]
    public void An_empty_selection_produces_no_chunks() =>
        Assert.Empty(Chunker.Split("xunit-v3-4", [], overhead: 200));

    [Fact]
    public void The_ceiling_is_detected_per_platform_and_is_never_a_flag()
    {
        // No adopter can reason about an 8,191-character limit, so a wrong value would be a bug
        // rather than a knob.
        Assert.Equal(OperatingSystem.IsWindows() ? 8_000 : 100_000, CommandLineCeiling.Characters);
    }

    // ---- The framework selector ---------------------------------------------------------------------

    [Theory]
    [InlineData(".NETCoreApp,Version=v10.0", null, "net10.0")]
    [InlineData(".NETCoreApp,Version=v8.0", null, "net8.0")]
    [InlineData(".NETStandard,Version=v2.0", null, "netstandard2.0")]
    [InlineData(".NETFramework,Version=v4.7.2", null, "net472")]
    [InlineData(".NETFramework,Version=v4.8", null, "net48")]
    public void A_moniker_without_a_platform_is_exact(string frameworkName, string? platform, string expected) =>
        Assert.Equal(
            expected,
            FrameworkSelector.Derive(TargetFrameworkMoniker.FromAttributes(frameworkName, platform)));

    [Fact]
    public void A_platform_suffixed_moniker_is_underivable()
    {
        // TargetPlatformAttribute always carries a version, so net10.0-windows reads back as
        // net10.0-windows7.0 — which -f rejects — and a project declaring net10.0-windows7.0
        // produces byte-identical attributes. No reconstruction rule can be correct for both.
        Assert.Null(
            FrameworkSelector.Derive(
                TargetFrameworkMoniker.FromAttributes(".NETCoreApp,Version=v10.0", "Windows7.0")));
    }

    [Fact]
    public void An_unreadable_framework_is_underivable() => Assert.Null(FrameworkSelector.Derive(null));
}
