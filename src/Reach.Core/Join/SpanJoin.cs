using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Reach.Changes;
using Reach.Graph;

namespace Reach.Join;

/// <summary>What one changed declaration reached in the call graph.</summary>
/// <param name="Joined">
/// False when nothing matched. A declaration that fails to join falls through to whole-assembly
/// widening for its project — over-selection, and it is what makes a phantom member from a
/// misparse survivable rather than a silent hole.
/// </param>
internal sealed record JoinResult(ChangedMember Member, IReadOnlyList<MethodId> Methods)
{
    internal bool Joined => Methods.Count > 0;
}

/// <summary>
/// Turns a changed declaration in source into method identities in the call graph, by matching
/// the declaration's source span against the sequence points debug symbols record.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Chosen over signature keys by measurement, not on paper.</strong> Both mechanisms
/// were built and run over the same fixture; the numbers and the reasoning are in ticket 10's
/// Answer. The short version is the row the ticket said outranks the others: this one's failure
/// mode is "no match, fall through to whole-assembly widening", and the other's was "matched
/// the wrong overload".
/// </para>
/// <para>
/// Several things the join owes fall out of being positional rather than needing rules of their
/// own: a property declaration's span contains its accessors' sequence points, an auto-property
/// initializer's span contains the constructor's sequence point for it, and a lambda or local
/// function sits inside its kernel method's span.
/// </para>
/// <para>
/// A function, not a C# interface. One adapter ships, and an interface with one implementation
/// forever is exactly the indirection that makes widening fan out in the codebase Reach will be
/// pointed at.
/// </para>
/// </remarks>
internal sealed class SpanJoin
{
    private readonly Dictionary<string, List<MethodExtent>> byDocument;
    private readonly string repositoryRoot;

    internal SpanJoin(IEnumerable<GraphAssembly> assemblies, string repositoryRoot)
    {
        this.repositoryRoot = Paths.Normalise(repositoryRoot);
        byDocument = Index(assemblies);
    }

    /// <summary>
    /// Every method whose IL came from inside <paramref name="member"/>'s declaration. Several
    /// when the declaration is multi-targeted, or when it contains generated members.
    /// </summary>
    internal JoinResult Resolve(ChangedMember member)
    {
        if (member.Span.IsEmpty)
        {
            return new JoinResult(member, []);
        }

        var path = Paths.Normalise(Path.Combine(repositoryRoot, member.Path));

        if (!byDocument.TryGetValue(path, out var candidates))
        {
            return new JoinResult(member, []);
        }

        return new JoinResult(
            member,
            [
                .. candidates
                    .Where(candidate =>
                        member.Span.Contains(candidate.StartLine, candidate.StartColumn)
                        && member.Span.Contains(candidate.EndLine, candidate.EndColumn))
                    .Select(candidate => candidate.Method)
                    .Distinct()
            ]);
    }

    internal IReadOnlyList<JoinResult> ResolveAll(IEnumerable<ChangedMember> members) =>
        [.. members.Select(Resolve)];

    /// <summary>
    /// Document path to the methods whose sequence points sit in it, built once. A method with
    /// no sequence points at all — abstract, <c>extern</c>, a <c>partial</c> declaration with
    /// no implementation — appears nowhere, which is how it falls through to widening.
    /// </summary>
    private static Dictionary<string, List<MethodExtent>> Index(IEnumerable<GraphAssembly> assemblies)
    {
        var index = new Dictionary<string, List<MethodExtent>>(Paths.Comparer);

        foreach (var assembly in assemblies)
        {
            var symbols = assembly.Symbols;

            if (symbols is null)
            {
                continue;
            }

            foreach (var handle in symbols.MethodDebugInformation)
            {
                var information = symbols.GetMethodDebugInformation(handle);

                if (information.SequencePointsBlob.IsNil)
                {
                    continue;
                }

                var method = MethodId.Definition(
                    assembly.Ordinal,
                    MetadataTokens.GetToken(handle.ToDefinitionHandle()));

                foreach (var (document, extent) in ExtentsOf(symbols, information, method))
                {
                    if (!index.TryGetValue(document, out var methods))
                    {
                        index[document] = methods = [];
                    }

                    methods.Add(extent);
                }
            }
        }

        return index;
    }

    /// <summary>
    /// One extent per document the method has sequence points in — more than one when a
    /// partial method's body and its declaration sit in different files.
    /// </summary>
    private static IEnumerable<(string Document, MethodExtent Extent)> ExtentsOf(
        MetadataReader symbols,
        MethodDebugInformation information,
        MethodId method)
    {
        var extents = new Dictionary<string, MethodExtent>(Paths.Comparer);

        foreach (var point in information.GetSequencePoints())
        {
            // A hidden sequence point carries line 0xFEEFEE and means "no source here".
            if (point.IsHidden || point.Document.IsNil)
            {
                continue;
            }

            var document = Paths.Normalise(symbols.GetString(symbols.GetDocument(point.Document).Name));

            extents[document] = extents.TryGetValue(document, out var existing)
                ? existing.Extend(point)
                : MethodExtent.Of(method, point);
        }

        return extents.Select(entry => (entry.Key, entry.Value));
    }

    /// <summary>The first and last place in one document a method's IL came from.</summary>
    private readonly record struct MethodExtent(
        MethodId Method,
        int StartLine,
        int StartColumn,
        int EndLine,
        int EndColumn)
    {
        internal static MethodExtent Of(MethodId method, SequencePoint point) =>
            new(method, point.StartLine, point.StartColumn, point.EndLine, point.EndColumn);

        internal MethodExtent Extend(SequencePoint point)
        {
            var (startLine, startColumn) = Before(point.StartLine, point.StartColumn, StartLine, StartColumn)
                ? (point.StartLine, point.StartColumn)
                : (StartLine, StartColumn);

            var (endLine, endColumn) = Before(EndLine, EndColumn, point.EndLine, point.EndColumn)
                ? (point.EndLine, point.EndColumn)
                : (EndLine, EndColumn);

            return this with
            {
                StartLine = startLine,
                StartColumn = startColumn,
                EndLine = endLine,
                EndColumn = endColumn,
            };
        }

        private static bool Before(int line, int column, int otherLine, int otherColumn) =>
            line < otherLine || (line == otherLine && column < otherColumn);
    }
}
