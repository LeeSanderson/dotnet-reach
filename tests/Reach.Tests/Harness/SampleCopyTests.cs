using Reach.Tests.Fixtures;
using Xunit.Sdk;

namespace Reach.Tests.Harness;

/// <summary>
/// How the harness fails before Reach runs: a patch that is missing or does not apply, and a
/// stray file in the copy. Over the real Basics sample, with patches written to a temp folder so
/// no broken patch is ever checked in. No build: these stop before it.
/// </summary>
public sealed class SampleCopyTests : IDisposable
{
    private const string Sample = "Basics";

    /// <summary>
    /// A patch that applies to Basics, so a test can name it as a baseline or as the first of
    /// several.
    /// </summary>
    private const string Good = "good.patch";

    /// <summary>
    /// Context that is not in <c>Discount.cs</c>, so <c>git apply</c> rejects it.
    /// </summary>
    private const string Broken =
        "--- a/src/Basics.Core/Discount.cs\r\n"
        + "+++ b/src/Basics.Core/Discount.cs\r\n"
        + "@@ -1,3 +1,3 @@\r\n"
        + " namespace Basics.Core;\r\n"
        + "-// a line Discount.cs never had\r\n"
        + "+// its replacement\r\n"
        + " \r\n";

    private readonly TempDirectory _patches = TempDirectory.Create("reach-patches");

    public SampleCopyTests() =>
        File.Copy(
            Path.Combine(SampleCopy.PatchFolderOf(Sample), "discount-body-change.patch"),
            _patches.Combine(Good));

    [Fact]
    public void A_missing_patch_fails_naming_it_by_role_with_the_fix_line()
    {
        using var copy = SampleCopy.Create(Sample, _patches.Path);

        var failure = Assert.Throws<FailException>(() => copy.ApplyChanges([Good, "missing.patch"]));

        Assert.StartsWith("2 of 2: missing.patch: no such patch at ", failure.Message, StringComparison.Ordinal);
        Assert.Contains(
            "To fix: dotnet run tests/tools/patch.cs -- apply Basics missing, resolve, "
            + "then dotnet run tests/tools/patch.cs -- save Basics missing --force.",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_patch_that_does_not_apply_fails_with_gits_verbose_output_and_the_fix_line()
    {
        _patches.WriteFile("broken.patch", Broken);

        using var copy = SampleCopy.Create(Sample, _patches.Path);

        var failure = Assert.Throws<FailException>(() => copy.ApplyChanges(["broken.patch"]));

        Assert.StartsWith("1 of 1: broken.patch did not apply.", failure.Message, StringComparison.Ordinal);

        // git's own report, with the context it searched for.
        Assert.Contains("Checking patch src/Basics.Core/Discount.cs", failure.Message, StringComparison.Ordinal);
        Assert.Contains("while searching for:", failure.Message, StringComparison.Ordinal);
        Assert.Contains("a line Discount.cs never had", failure.Message, StringComparison.Ordinal);

        Assert.Contains("-- apply Basics broken, resolve", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_baseline_patch_that_does_not_apply_is_named_as_the_baseline()
    {
        _patches.WriteFile("broken.patch", Broken);

        var failure = Assert.Throws<FailException>(() => SampleCopy.Create(Sample, _patches.Path, "broken.patch"));

        Assert.StartsWith("baseline: broken.patch did not apply.", failure.Message, StringComparison.Ordinal);
        Assert.Contains("-- apply Basics broken, resolve", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fix_line_for_a_change_patch_on_a_baseline_names_the_baseline()
    {
        _patches.WriteFile("broken.patch", Broken);

        using var copy = SampleCopy.Create(Sample, _patches.Path, Good);

        var failure = Assert.Throws<FailException>(() => copy.ApplyChanges(["broken.patch"]));

        Assert.Contains(
            "-- apply Basics broken --on good, resolve, then dotnet run tests/tools/patch.cs -- save Basics broken --on good --force.",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_status_check_passes_when_only_the_patched_paths_changed()
    {
        using var copy = SampleCopy.Create(Sample, _patches.Path);

        copy.ApplyChanges([Good]);
        copy.AssertOnlyPatchedPathsChanged();
    }

    [Fact]
    public void A_stray_file_in_the_copy_fails_the_status_check()
    {
        using var copy = SampleCopy.Create(Sample, _patches.Path);

        copy.ApplyChanges([Good]);
        File.WriteAllText(Path.Combine(copy.Path, "stray.txt"), "not from a patch");

        var failure = Assert.ThrowsAny<XunitException>(copy.AssertOnlyPatchedPathsChanged);

        Assert.Contains("git status lists paths no change patch touched", failure.Message, StringComparison.Ordinal);
        Assert.Contains("stray.txt", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Discount.cs", failure.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _patches.Dispose();
}
