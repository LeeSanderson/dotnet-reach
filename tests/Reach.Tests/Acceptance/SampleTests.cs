namespace Reach.Tests.Acceptance;

/// <summary>
/// The base of every sample's acceptance-test class, which names its sample here once.
/// </summary>
/// <remarks>
/// <para>
/// A test reads <c>Apply("x.patch").ExpectAsync(new() { ... })</c>, or
/// <c>Baseline("b.patch").Apply("x.patch").ExpectAsync(...)</c> for a variant starting state. Patches
/// are named relative to <c>tests/Reach.Tests/Acceptance/&lt;Sample&gt;/</c>.
/// </para>
/// <para>
/// The shape of the calls is the rule: <c>Baseline</c> comes first and at most once, because
/// only this class has it; <c>Apply</c> takes at least one patch, because its first parameter is
/// not optional; and only what <c>Apply</c> returns has <c>ExpectAsync</c>.
/// </para>
/// </remarks>
public abstract class SampleTests(string sample)
{
    /// <summary>
    /// The two notices every run carries: the harness passes the baseline commit as
    /// <c>--base</c>, and leaves its change patches uncommitted, so <c>HEAD</c> is the baseline.
    /// </summary>
    private protected static readonly IReadOnlyList<string> EveryRunNotices = ["baseline-resolved", "baseline-is-head"];

    /// <summary>
    /// Applies <paramref name="patch"/> before the baseline commit, so it is part of the baseline
    /// and Reach never sees it as a change.
    /// </summary>
    private protected BaselinedSample Baseline(string patch) =>
        new(sample, SampleCopy.PatchFolderOf(sample), patch);

    /// <summary>
    /// Applies change patches after the baseline commit, in order, left uncommitted.
    /// </summary>
    private protected PatchedSample Apply(string patch, params string[] more) =>
        new(sample, SampleCopy.PatchFolderOf(sample), BaselinePatch: null, [patch, .. more]);
}

/// <summary>
/// A sample with its baseline patch named, waiting for its change patches.
/// </summary>
internal sealed record BaselinedSample(string Sample, string PatchFolder, string BaselinePatch)
{
    internal PatchedSample Apply(string patch, params string[] more) =>
        new(Sample, PatchFolder, BaselinePatch, [patch, .. more]);
}

/// <summary>
/// A sample with every patch named, ready to run and compare.
/// </summary>
internal sealed record PatchedSample(
    string Sample,
    string PatchFolder,
    string? BaselinePatch,
    IReadOnlyList<string> ChangePatches)
{
    /// <summary>
    /// Runs the harness and Reach twice, then compares every given section of
    /// <paramref name="expected"/> and checks every invariant, failing once with all the
    /// differences.
    /// </summary>
    internal async Task ExpectAsync(Expected expected)
    {
        using var copy = SampleCopy.Create(Sample, PatchFolder, BaselinePatch);

        copy.ApplyChanges(ChangePatches);
        copy.AssertOnlyPatchedPathsChanged();

        var observation = await copy.RunTwiceAsync(TestContext.Current.CancellationToken);

        if (Expectation.Failures(expected, observation) is { } failures)
        {
            Assert.Fail(failures);
        }
    }
}
