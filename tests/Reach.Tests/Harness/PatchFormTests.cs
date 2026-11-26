using Reach.Tests.Fixtures;

namespace Reach.Tests.Harness;

/// <summary>
/// The stripped-form guard: a checked-in patch holds only <c>---</c>/<c>+++</c> file headers and
/// <c>@@</c> hunks. Outside the acceptance namespace, so it runs in the fast CI step. See the
/// <see href="https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-layer/spec.md#31-the-format">acceptance-layer spec §3.1</see>.
/// </summary>
public sealed class PatchFormTests
{
    private const string Stripped =
        "--- a/src/Basics.Core/Discount.cs\r\n"
        + "+++ b/src/Basics.Core/Discount.cs\r\n"
        + "@@ -1,3 +1,3 @@ public static class Discount\r\n"
        + " namespace Basics.Core;\r\n"
        + "-// old\r\n"
        + "+// new\r\n"
        + " \r\n";

    [Fact]
    public void Every_checked_in_patch_is_in_the_stripped_form()
    {
        var acceptance = RepositoryRoot.Combine("tests", "Reach.Tests", "Acceptance");

        var failures = Directory
            .EnumerateFiles(acceptance, "*.patch", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .SelectMany(patch => PatchForm.Failures(Path.GetRelativePath(acceptance, patch), File.ReadAllText(patch)))
            .ToArray();

        Assert.True(failures.Length == 0, string.Join("\n\n", failures));
    }

    [Fact]
    public void A_stripped_patch_passes()
    {
        Assert.Empty(PatchForm.Failures("Basics/discount-body-change.patch", Stripped));
    }

    [Fact]
    public void A_patch_with_an_index_line_fails_naming_the_file_and_the_save_command()
    {
        var patch = "index abc..def 100644\r\n" + Stripped;

        var failure = Assert.Single(PatchForm.Failures("Basics/discount-body-change.patch", patch));

        Assert.Equal(
            "Basics/discount-body-change.patch is not in the stripped form. Line 1: index abc..def 100644\n"
            + "  Only ---/+++ file headers and @@ hunks belong in a checked-in patch. To fix, apply it and save it again:\n"
            + "  dotnet run tests/tools/patch.cs -- apply Basics discount-body-change\n"
            + "  dotnet run tests/tools/patch.cs -- save Basics discount-body-change --force\n"
            + "  (add --on <baseline> to both if it was written on a baseline patch)",
            failure);
    }

    [Theory]
    [InlineData("diff --git a/src/Basics.Core/Discount.cs b/src/Basics.Core/Discount.cs")]
    [InlineData("index abc..def")]
    [InlineData("new file mode 100644")]
    [InlineData("deleted file mode 100644")]
    [InlineData("old mode 100644")]
    [InlineData("new mode 100755")]
    [InlineData("similarity index 90%")]
    [InlineData("rename from src/Old.cs")]
    public void A_git_extended_header_line_fails(string line)
    {
        var failure = Assert.Single(PatchForm.Failures(Path.Combine("Basics", "row.patch"), line + "\r\n" + Stripped));

        Assert.Contains("Line 1: " + line + "\n", failure, StringComparison.Ordinal);
        Assert.Contains("save Basics row --force", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_offending_line_is_listed_in_one_failure()
    {
        var patch = "diff --git a/x b/x\r\nindex abc..def\r\n" + Stripped;

        var failure = Assert.Single(PatchForm.Failures("Basics/row.patch", patch));

        Assert.Contains("Line 1: diff --git a/x b/x\n", failure, StringComparison.Ordinal);
        Assert.Contains("Line 2: index abc..def\n", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Hunk_content_that_looks_like_a_header_passes()
    {
        var patch = Stripped + "+index abc..def\r\n- diff --git\r\n";

        Assert.Empty(PatchForm.Failures("Basics/row.patch", patch));
    }
}
