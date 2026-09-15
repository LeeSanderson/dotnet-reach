namespace Reach.Projects;

/// <summary>What Reach was pointed at: a solution, or a single project.</summary>
/// <remarks>
/// The two are different <em>analysis scopes</em>, not two ways of naming the same work,
/// which is why discovery refuses to guess between them.
/// </remarks>
internal sealed record DiscoveredTarget(string Path, TargetKind Kind)
{
    internal string Directory => System.IO.Path.GetDirectoryName(Path)!;

    internal string Name => System.IO.Path.GetFileName(Path);
}

internal enum TargetKind
{
    Solution,
    Project,
}

/// <summary>
/// Discovery's answer: a target, or the exit-1 message naming what was found. Ambiguity is
/// always an error — recursion and coin flips are where "confidently wrong" lives.
/// </summary>
internal sealed record TargetDiscoveryResult(DiscoveredTarget? Target, string Message)
{
    internal bool Discovered => Target is not null;

    internal ExitCode ExitCode => Discovered ? ExitCode.Success : ExitCode.UsageError;

    internal static TargetDiscoveryResult Found(string path, TargetKind kind) =>
        new(new DiscoveredTarget(System.IO.Path.GetFullPath(path), kind), string.Empty);

    internal static TargetDiscoveryResult Ambiguous(string message) => new(null, message);
}
