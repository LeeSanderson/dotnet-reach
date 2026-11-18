using System.Diagnostics;

namespace Reach.Reporting;

/// <summary>
/// Reach's own phase timings, always emitted — the instrument already exists at zero extra
/// cost, and the first real run is the first datapoint the performance budget gets.
/// </summary>
/// <remarks>
/// Segregated into the envelope's one clearly-marked object, so that excluding a single key
/// makes two reports comparable.
/// </remarks>
public sealed class PhaseTimings(TimeProvider clock)
{
    private readonly Dictionary<string, long> _phases = new(StringComparer.Ordinal);
    private readonly Stopwatch _total = Stopwatch.StartNew();

    /// <summary>
    /// Starts timing against the system clock.
    /// </summary>
    public PhaseTimings()
        : this(TimeProvider.System)
    {
    }

    internal DateTimeOffset StartedUtc { get; } = clock.GetUtcNow();

    /// <summary>
    /// Times <paramref name="work"/> and records it under <paramref name="phase"/>.
    /// </summary>
    internal async Task<T> MeasureAsync<T>(string phase, Func<Task<T>> work)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            Record(phase, timer.Elapsed);
        }
    }

    internal T Measure<T>(string phase, Func<T> work)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            return work();
        }
        finally
        {
            Record(phase, timer.Elapsed);
        }
    }

    internal void Record(string phase, TimeSpan elapsed) =>
        _phases[phase] = _phases.GetValueOrDefault(phase) + (elapsed.Ticks / TimeSpan.TicksPerMillisecond);

    internal ReportTimings ToReport() =>
        new()
        {
            StartedUtc = StartedUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            TotalMs = _total.Elapsed.Ticks / TimeSpan.TicksPerMillisecond,
            Phases = new SortedDictionary<string, long>(_phases, StringComparer.Ordinal),
        };
}
