using Microsoft.CodeAnalysis.CSharp;
using Reach.Changes;
using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Changes;

/// <summary>
/// Detecting the C# Reach cannot read. The naive rule — any parse error widens — is not a safety
/// net, because Roslyn checks language versions at binding rather than at parsing and Reach only
/// ever calls <c>ParseText</c>.
/// </summary>
public class ParseHealthTests
{
    [Fact]
    public void The_parser_ceiling_is_preview_and_never_latest()
    {
        // The highest-value line in this whole area, and it is one argument. `Latest` pins the
        // parser to the shipped language version and misparses anything newer, silently.
        Assert.Equal(LanguageVersion.Preview, ParseHealthCheck.ParserCeiling);
    }

    [Fact]
    public void Sound_source_is_sound() =>
        Assert.True(ParseHealthCheck.Of("namespace N; public class C { public int M() => 1; }").IsSound);

    [Fact]
    public void A_syntax_error_is_detected()
    {
        var health = ParseHealthCheck.Of("namespace N; public class C { public int M() => ");

        Assert.True(health.HasErrorDiagnostic);
        Assert.False(health.IsSound);
    }

    [Fact]
    public void Skipped_tokens_are_detected_as_a_structural_signal()
    {
        // A structural signal rather than a diagnostic one, and it earns its own place: a
        // construct whose members are swallowed never puts a changed method into the changed
        // set at all.
        var health = ParseHealthCheck.Of(
            """
            namespace N;

            public class C
            {
                public int M() => 1;
            }

            } } }
            """);

        Assert.False(health.IsSound);
    }

    [Fact]
    public void Empty_source_is_sound() => Assert.True(ParseHealthCheck.Of(string.Empty).IsSound);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("preview")]
    [InlineData("latest")]
    [InlineData("default")]
    [InlineData("12.0")]
    [InlineData("13.0")]
    public void A_language_version_at_or_below_the_ceiling_is_not_reported(string? declared) =>
        Assert.False(ParseHealthCheck.ExceedsCeiling(declared));

    [Fact]
    public void A_language_version_above_the_ceiling_is_reported_and_does_not_widen()
    {
        // The notice exists; widening does not. With current Roslyn shipped the window is
        // narrow, and widening on it would fire across whole modern codebases for a hazard that
        // usually is not present.
        Assert.True(ParseHealthCheck.ExceedsCeiling("99.0"));
        Assert.Equal("langversion-above-ceiling", NoticeCodes.LangVersionAboveCeiling);
    }

    [Fact]
    public async Task A_file_that_does_not_parse_widens_its_project_and_says_so()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", "namespace N; public class Widget { public int Spin() => 1; }");
        var baseline = repository.CommitBaseline();

        repository.Write("src/Core/Widget.cs", "namespace N; public class Widget { public int Spin() => ");

        var changes = await repository.ChangesAsync(baseline, TestContext.Current.CancellationToken);

        var notice = Assert.Single(changes.Notices, n => n.Code == NoticeCodes.ParseFailed);

        Assert.Equal(NoticeKind.BlindSpot, notice.Kind);
        Assert.Contains("src/Core/Widget.cs", notice.Message);

        // Widened rather than read as unchanged: a file Reach could not parse is a file whose
        // members it cannot trust.
        Assert.Contains(changes.AssemblyWidenings, widening => widening.Path == "src/Core/Widget.cs");
    }

    [Fact]
    public async Task A_file_that_parses_cleanly_emits_no_parse_notice()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", "namespace N; public class Widget { public int Spin() => 1; }");
        var baseline = repository.CommitBaseline();

        repository.Write("src/Core/Widget.cs", "namespace N; public class Widget { public int Spin() => 2; }");

        var changes = await repository.ChangesAsync(baseline, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(changes.Notices, n => n.Code == NoticeCodes.ParseFailed);
    }
}
