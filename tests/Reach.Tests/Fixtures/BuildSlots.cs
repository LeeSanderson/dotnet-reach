using System.Globalization;

namespace Reach.Tests.Fixtures;

/// <summary>
/// A machine-wide limit on how many sample builds run at once, shared by every test process on
/// the machine: two worktrees running the suite side by side share the same slots.
/// </summary>
/// <remarks>
/// <para>
/// Without it, every acceptance test builds at once (the suite runs one test per processor), and
/// each <c>dotnet build</c> starts one MSBuild node per processor. That is the square of the core
/// count in processes, multiplied by every concurrent test run, and it exhausted a developer
/// machine's memory.
/// </para>
/// <para>
/// A slot is an exclusive lock on a file in the temp directory. The operating system drops the
/// lock when the process exits, however it exits, so a killed run never leaks a slot.
/// <c>REACH_BUILD_SLOTS</c> overrides the count.
/// </para>
/// </remarks>
internal static class BuildSlots
{
    private const string CountVariable = "REACH_BUILD_SLOTS";

    private const int MaximumDefault = 4;

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

    private static readonly string Directory = Path.Combine(Path.GetTempPath(), "reach-build-slots");

    /// <summary>
    /// How many sample builds may run at once on this machine.
    /// </summary>
    internal static int Count { get; } = CountFromEnvironment() ?? Math.Clamp(Environment.ProcessorCount / 2, 1, MaximumDefault);

    /// <summary>
    /// The MSBuild node count each build may use, so that all slots together use about one node
    /// per processor.
    /// </summary>
    internal static int NodesPerBuild { get; } = Math.Max(1, Environment.ProcessorCount / Count);

    /// <summary>
    /// Waits for a free slot and holds it until the result is disposed.
    /// </summary>
    internal static async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        System.IO.Directory.CreateDirectory(Directory);

        while (true)
        {
            for (var slot = 0; slot < Count; slot++)
            {
                if (TryLock(Path.Combine(Directory, $"{slot}.lock")) is { } held)
                {
                    return held;
                }
            }

            await Task.Delay(Poll, cancellationToken);
        }
    }

    private static FileStream? TryLock(string path)
    {
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            // Another build holds this slot.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Windows reports a file held open for deletion this way; treat it as taken.
            return null;
        }
    }

    private static int? CountFromEnvironment() =>
        int.TryParse(Environment.GetEnvironmentVariable(CountVariable), NumberStyles.None, CultureInfo.InvariantCulture, out var count) && count > 0
            ? count
            : null;
}
