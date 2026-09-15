using System.Text;

namespace Reach.Rendering;

/// <summary>
/// The filter expression grammar accepted by one combination of test framework, framework
/// version and runner host.
/// </summary>
/// <remarks>
/// M1 renders for <c>dotnet test</c> only, and that is not a gap left open — it is what keeps
/// the exit-8 claim honest. Rendering the native dialect as well would manufacture the one
/// genuinely silent cross-host failure: the two hosts' option names intersect in exactly four,
/// and MTP normalises a single dash to a double, so <c>-filter</c> is accepted by both hosts in
/// different filter languages with no diagnostic either side.
/// </remarks>
internal static class FilterDialect
{
    /// <summary>
    /// Characters the VSTest filter grammar treats as syntax. A test name carrying one is
    /// escaped with a backslash rather than quoted, because there is no quoting in the grammar.
    /// </summary>
    private const string Special = @"\()&|=!~,";

    /// <summary>
    /// The operator this dialect matches with.
    /// </summary>
    /// <remarks>
    /// <strong>NUnit renders with <c>~</c> (contains), never equality.</strong> NUnit's VSTest
    /// <c>FullyQualifiedName</c> <em>includes</em> a parameterised test's arguments —
    /// <c>Ns.C.MyTest(1,2)</c> — so equality ought to match nothing, and works only because the
    /// adapter re-parses the filter into NUnit filter XML where matching is parent-aware. That
    /// chain breaks under <c>UseNUnitFilter=false</c>, under <c>DiscoveryMethod.Legacy</c>, and
    /// on the IDE path. Reach can see none of those and cannot detect the failure either — an
    /// under-selected theory simply does not run — so this follows widen-when-uncertain.
    /// </remarks>
    internal static string OperatorFor(string dialect) =>
        dialect.StartsWith("nunit", StringComparison.Ordinal) ? "~" : "=";

    /// <summary>Whether this dialect's operator can match tests the selection did not name.</summary>
    internal static bool OverMatches(string dialect) => OperatorFor(dialect) == "~";

    /// <summary>
    /// The host a project on this dialect answers to under <c>dotnet test</c>, and therefore
    /// which private channel is available for a long selection.
    /// </summary>
    internal static bool UsesTestingPlatform(string dialect) =>
        dialect.StartsWith("xunit-v3", StringComparison.Ordinal);

    internal static string Clause(string dialect, string fullyQualifiedName) =>
        "FullyQualifiedName" + OperatorFor(dialect) + Escape(fullyQualifiedName);

    internal static string Expression(string dialect, IEnumerable<string> fullyQualifiedNames) =>
        string.Join("|", fullyQualifiedNames.Select(name => Clause(dialect, name)));

    internal static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length + 8);

        foreach (var character in value)
        {
            if (Special.Contains(character, StringComparison.Ordinal))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Which enumerated tests a rendered filter actually matches, by applying the dialect's own
    /// semantics back over the list.
    /// </summary>
    /// <remarks>
    /// The canonical selection and the rendered filter deliberately disagree, because of the
    /// <c>~</c> rule. Cheap, because every test method is already enumerated to produce the
    /// total — and without it the over-selection measurement would read the wrong number.
    /// </remarks>
    internal static IReadOnlyList<string> Matches(
        string dialect,
        IEnumerable<string> selected,
        IEnumerable<string> allTests)
    {
        var names = selected.ToArray();

        if (!OverMatches(dialect))
        {
            var exact = names.ToHashSet(StringComparer.Ordinal);

            return [.. allTests.Where(exact.Contains).Order(StringComparer.Ordinal)];
        }

        return
        [
            .. allTests
                .Where(candidate => names.Any(name => candidate.Contains(name, StringComparison.Ordinal)))
                .Order(StringComparer.Ordinal)
        ];
    }
}
