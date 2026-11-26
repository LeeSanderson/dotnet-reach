using Reach.Reporting;

namespace Reach.Tests.Acceptance;

/// <summary>
/// Everything an acceptance test observed: Reach run twice over the same patched sample.
/// </summary>
/// <param name="First">The first run.</param>
/// <param name="Second">The second run, over the same input, for the determinism invariant.</param>
/// <param name="TempRoot">The test's temp root, which the determinism scrubber replaces.</param>
/// <param name="BuildFailure">
/// The tail of the harness's own build when it failed, so Reach ran in build mode. Null when it
/// succeeded.
/// </param>
internal sealed record Observation(ReachRun First, ReachRun Second, string TempRoot, string? BuildFailure);

/// <summary>
/// One Reach run as the harness saw it: what it exited with, and the report it wrote.
/// </summary>
internal sealed record ReachRun(int ExitCode, Report Report);
