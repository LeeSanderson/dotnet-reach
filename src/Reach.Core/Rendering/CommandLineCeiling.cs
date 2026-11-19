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
    /// Under <c>cmd</c>'s documented 8,191-character limit, with room to spare (ADR-0009).
    /// </summary>
    private const int WindowsCharacters = 8_000;

    /// <summary>
    /// Clear of Linux's 128 KiB <c>MAX_ARG_STRLEN</c> for a single argument, with headroom for
    /// the rest of the command line (ADR-0009).
    /// </summary>
    private const int ElsewhereCharacters = 100_000;

    /// <summary>
    /// What an argument costs beyond its own text: a separating space and a pair of quotes.
    /// </summary>
    private const int PerArgumentOverhead = 3;

    /// <summary>
    /// Windows takes the lower of the two limits, because Reach cannot know whether the argv it
    /// emits will be issued through <c>cmd</c> — and being wrong the other way is a build that
    /// fails at the test step with a message about the command line being too long.
    /// </summary>
    internal static int Characters { get; } = OperatingSystem.IsWindows() ? WindowsCharacters : ElsewhereCharacters;

    internal static bool Fits(IReadOnlyList<string> arguments) =>
        arguments.Sum(argument => argument.Length + PerArgumentOverhead) <= Characters;
}
