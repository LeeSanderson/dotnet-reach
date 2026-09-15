namespace Reach.Rendering;

/// <summary>
/// How long a rendered command line may be before it has to be delivered another way.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The ceiling is the normal path, not an edge case.</strong> A
/// <c>FullyQualifiedName=…</c> clause runs 60–90 characters, so <c>cmd</c>'s 8,191 limit arrives
/// at roughly 100 selected test methods and <c>CreateProcessW</c>'s 32,767 at roughly 400. An
/// ordinary pull request on a large solution clears both.
/// </para>
/// <para>
/// Detected per platform and <strong>never a flag</strong>: no adopter can reason about an
/// 8,191-character limit, so a wrong value would be a bug rather than a knob.
/// </para>
/// </remarks>
internal static class CommandLineCeiling
{
    /// <summary>
    /// Windows takes the lower of the two limits, because Reach cannot know whether the argv it
    /// emits will be issued through <c>cmd</c> — and being wrong the other way is a build that
    /// fails at the test step with a message about the command line being too long.
    /// </summary>
    internal static int Characters { get; } = OperatingSystem.IsWindows() ? 8_000 : 100_000;

    internal static bool Fits(IReadOnlyList<string> arguments) =>
        arguments.Sum(argument => argument.Length + 3) <= Characters;
}
