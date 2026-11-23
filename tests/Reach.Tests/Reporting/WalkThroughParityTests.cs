using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Reporting;

/// <summary>
/// Every value the walk-through in <c>docs/report-schema.md</c> quotes from
/// <c>docs/example-report.json</c> equals the example.
/// </summary>
/// <remarks>
/// <para>
/// <c>ExampleReportTests</c> keeps the example equal to a real run, so the example cannot rot. The
/// prose about it can: regenerating the example reorders entries and changes counts without
/// touching a word of the walk-through. This closes that gap.
/// </para>
/// <para>
/// The prose marks each quoted value with an HTML comment, invisible on GitHub. The convention
/// is explained in a comment at the top of the walk-through itself:
/// <c>&lt;!-- example: PATH = VALUE --&gt;</c> right after the value it pins, or
/// <c>&lt;!-- example: PATH --&gt;</c> on the line before a <c>json</c> block, whose every field
/// must then equal the example's at <c>PATH</c>.
/// </para>
/// </remarks>
public sealed partial class WalkThroughParityTests
{
    private const string Section = "## Reading a surprising selection";
    private const string Ellipsis = "…";

    [Fact]
    public void Every_value_the_walk_through_quotes_is_the_example_s() =>
        Assert.Empty(Mismatches(WalkThrough(Schema()), Example()));

    [Fact]
    public void One_changed_value_in_the_walk_through_fails_the_comparison()
    {
        // Asserted by construction rather than by trusting the test above: the NUnit entry's
        // over-match is the number the walk-through exists to explain.
        var perturbed = Schema().Replace(
            "\"counts\": { \"selected\": 1, \"willRun\": 2, \"total\": 3 }",
            "\"counts\": { \"selected\": 1, \"willRun\": 3, \"total\": 3 }",
            StringComparison.Ordinal);

        var mismatch = Assert.Single(Mismatches(WalkThrough(perturbed), Example()));

        Assert.Contains("entries[1]", mismatch, StringComparison.Ordinal);
    }

    private static List<string> Mismatches(IReadOnlyList<string> walkThrough, JsonNode example)
    {
        var mismatches = new List<string>();
        var markers = 0;
        var number = 0;

        while (number < walkThrough.Count)
        {
            var line = walkThrough[number];
            var where = $"walk-through line {number + 1}";
            number++;

            if (BlockMarker.Match(line) is { Success: true } block)
            {
                markers++;
                var (fragment, next) = FencedJson(walkThrough, number);

                mismatches.AddRange(fragment is null
                    ? [$"{where}: the marker for {block.Groups["path"].Value} is not followed by a json block."]
                    : Check(example, block.Groups["path"].Value, fragment).Select(problem => $"{where}: {problem}"));

                number = next;
                continue;
            }

            if (line.StartsWith("```json", StringComparison.Ordinal))
            {
                mismatches.Add($"{where}: a json block with no marker naming what it quotes.");
                continue;
            }

            foreach (Match inline in InlineMarker.Matches(line))
            {
                markers++;
                var value = Parse(inline.Groups["value"].Value);

                mismatches.AddRange(value is null
                    ? [$"{where}: the value in the marker for {inline.Groups["path"].Value} is not JSON."]
                    : Check(example, inline.Groups["path"].Value, value).Select(problem => $"{where}: {problem}"));
            }
        }

        // Otherwise a walk-through that lost its markers passes by having nothing to check.
        Assert.True(markers > 0, "The walk-through carries no example markers.");

        return mismatches;
    }

    /// <summary>
    /// The json block starting at <paramref name="start"/>, and the index of the line after its
    /// closing fence. A bare property, such as <c>"counts": { … }</c>, is read as an object
    /// holding it.
    /// </summary>
    private static (JsonNode? Fragment, int Next) FencedJson(IReadOnlyList<string> lines, int start)
    {
        if (start >= lines.Count || !lines[start].StartsWith("```json", StringComparison.Ordinal))
        {
            return (null, start);
        }

        var end = start + 1;

        while (end < lines.Count && !lines[end].StartsWith("```", StringComparison.Ordinal))
        {
            end++;
        }

        var text = string.Join('\n', lines.Skip(start + 1).Take(end - start - 1)).Trim();

        return (Parse(text.StartsWith('"') ? $"{{ {text} }}" : text), end + 1);
    }

    /// <summary>
    /// Checks one marker. <c>count(PATH)</c> is how many values the path reaches; any other path
    /// must reach exactly one.
    /// </summary>
    private static IEnumerable<string> Check(JsonNode example, string path, JsonNode expected)
    {
        if (Count.Match(path) is { Success: true } count)
        {
            var reached = Select(example, count.Groups[1].Value);

            if (reached is null)
            {
                return [$"{path} is not a path this test can read."];
            }

            return expected.GetValueKind() == System.Text.Json.JsonValueKind.Number && expected.GetValue<int>() == reached.Count
                ? []
                : [$"{path} is {reached.Count} in the example, but the walk-through says {expected.ToJsonString()}."];
        }

        var nodes = Select(example, path);

        if (nodes is null)
        {
            return [$"{path} is not a path this test can read."];
        }

        if (nodes.Count != 1)
        {
            return [$"{path} reaches {nodes.Count} values in the example, not one."];
        }

        return Matches(expected, nodes[0])
            ? []
            : [$"{path} is {nodes[0]?.ToJsonString() ?? "null"} in the example, but the walk-through says {expected.ToJsonString()}."];
    }

    /// <summary>
    /// The values a path reaches, or <see langword="null"/> when the path is unreadable. Steps:
    /// <c>name</c>, <c>[n]</c>, <c>[*]</c> for every element, <c>[?key]</c> for the elements
    /// that have <c>key</c>, and <c>[?key=text]</c> for those whose <c>key</c> is that string.
    /// </summary>
    private static List<JsonNode?>? Select(JsonNode root, string path)
    {
        var steps = Step.Matches(path);

        if (steps.Sum(step => step.Length) != path.Length)
        {
            return null;
        }

        List<JsonNode?> nodes = [root];

        foreach (Match step in steps)
        {
            nodes = [.. nodes.SelectMany(node => Apply(node, step))];
        }

        return nodes;
    }

    private static IEnumerable<JsonNode?> Apply(JsonNode? node, Match step)
    {
        if (step.Groups["name"].Success)
        {
            return node is JsonObject named && named.TryGetPropertyValue(step.Groups["name"].Value, out var child)
                ? [child]
                : [];
        }

        if (node is not JsonArray array)
        {
            return [];
        }

        if (step.Groups["index"].Success)
        {
            var index = int.Parse(step.Groups["index"].Value, System.Globalization.CultureInfo.InvariantCulture);

            return index < array.Count ? [array[index]] : [];
        }

        if (step.Groups["key"].Success)
        {
            var key = step.Groups["key"].Value;
            var wanted = step.Groups["text"];

            return array.Where(element =>
                element is JsonObject candidate
                && candidate.TryGetPropertyValue(key, out var value)
                && (!wanted.Success || IsString(value, wanted.Value)));
        }

        return array;
    }

    private static bool IsString(JsonNode? node, string text) =>
        node?.GetValueKind() == System.Text.Json.JsonValueKind.String
        && string.Equals(node.GetValue<string>(), text, StringComparison.Ordinal);

    /// <summary>
    /// Whether every field the walk-through quotes equals the example's. An object need quote
    /// only some of its fields; an array quotes all its elements. In a string, <c>…</c> stands
    /// for any run of characters, which is how the prose elides what it does not need.
    /// </summary>
    private static bool Matches(JsonNode? expected, JsonNode? actual) => expected switch
    {
        JsonObject fields => actual is JsonObject found
            && fields.All(field => found.TryGetPropertyValue(field.Key, out var value) && Matches(field.Value, value)),
        JsonArray elements => actual is JsonArray found
            && elements.Count == found.Count
            && elements.Zip(found).All(pair => Matches(pair.First, pair.Second)),
        JsonValue elided when elided.GetValueKind() == System.Text.Json.JsonValueKind.String
            && elided.GetValue<string>().Contains(Ellipsis, StringComparison.Ordinal) =>
            actual?.GetValueKind() == System.Text.Json.JsonValueKind.String
            && Glob(elided.GetValue<string>()).IsMatch(actual.GetValue<string>()),
        _ => JsonNode.DeepEquals(expected, actual),
    };

    private static Regex Glob(string pattern) =>
        new(
            "^" + string.Join(".*", pattern.Split(Ellipsis).Select(Regex.Escape)) + "$",
            RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

    private static JsonNode? Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^<!-- example: (?<path>\S+) -->$")]
    private static partial Regex BlockMarker { get; }

    [GeneratedRegex(@"<!-- example: (?<path>\S+) = (?<value>.+?) -->")]
    private static partial Regex InlineMarker { get; }

    [GeneratedRegex(@"^count\((.+)\)$")]
    private static partial Regex Count { get; }

    [GeneratedRegex(@"\G(?:\.?(?<name>[A-Za-z]+)|\[(?<index>[0-9]+)\]|\[\*\]|\[\?(?<key>[A-Za-z]+)(?:=(?<text>[^\]]+))?\])")]
    private static partial Regex Step { get; }

    /// <summary>
    /// The lines from the walk-through's heading to the next <c>##</c> heading.
    /// </summary>
    private static List<string> WalkThrough(string schema)
    {
        var lines = schema.Split('\n');
        var start = Array.IndexOf(lines, Section);

        Assert.True(start >= 0, $"docs/report-schema.md has no \"{Section}\" heading.");

        return [.. lines.Skip(start + 1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))];
    }

    private static string Schema() => Read("report-schema.md");

    private static JsonNode Example() =>
        JsonNode.Parse(Read("example-report.json"))
        ?? throw new InvalidOperationException("docs/example-report.json is null.");

    private static string Read(string name)
    {
        var path = RepositoryRoot.Combine("docs", name);

        Assert.True(File.Exists(path), $"Nothing at {path}.");

        return File.ReadAllText(path).ReplaceLineEndings("\n");
    }
}
