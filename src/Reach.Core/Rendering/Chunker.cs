namespace Reach.Rendering;

/// <summary>
/// Splits a selection into as many filter expressions as the command line can carry.
/// </summary>
/// <remarks>
/// Every chunk carries at least one test by construction, which is half of why no path through
/// Reach's design renders a filter matching nothing.
/// </remarks>
internal static class Chunker
{
    /// <summary>
    /// Partitions <paramref name="names"/> so that each part's rendered expression fits under
    /// the ceiling, leaving <paramref name="overhead"/> characters for the rest of the argv.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<string>> Split(
        string dialect,
        IReadOnlyList<string> names,
        int overhead)
    {
        if (names.Count == 0)
        {
            return [];
        }

        var budget = Math.Max(CommandLineCeiling.Characters - overhead, 64);
        var chunks = new List<IReadOnlyList<string>>();
        var current = new List<string>();
        var length = 0;

        foreach (var name in names)
        {
            // The separator costs a character once there is something to separate from.
            var cost = FilterDialect.Clause(dialect, name).Length + (current.Count > 0 ? 1 : 0);

            if (current.Count > 0 && length + cost > budget)
            {
                chunks.Add(current);
                current = [];
                length = 0;
                cost = FilterDialect.Clause(dialect, name).Length;
            }

            current.Add(name);
            length += cost;
        }

        chunks.Add(current);

        return chunks;
    }
}
