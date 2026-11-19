using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reach.Changes;

/// <summary>
/// Reads one revision of one C# file into the types it declares. With
/// <see cref="MemberDeclarations"/>, which reads each member, the only place in Reach that
/// touches Roslyn.
/// </summary>
/// <remarks>
/// <para>
/// Roslyn parses changed files and <strong>never loads a solution</strong>. No
/// <c>MSBuildWorkspace</c>, ever — the tool has to work under <c>--no-build</c>, offline, in a
/// step whose whole purpose is to be cheap.
/// </para>
/// <para>
/// <strong>Parsed with <see cref="LanguageVersion.Preview"/>, never <c>Latest</c>.</strong>
/// Roslyn checks language versions at binding rather than at parsing, and Reach only ever
/// calls <c>ParseText</c>, so an older parser meeting newer C# never throws — it misparses
/// silently. <c>record Person(string First)</c> on an older parser reads as a <em>method</em>
/// named <c>Person</c>, cleanly and with no diagnostic. <c>Preview</c> is the one argument
/// that makes that window as narrow as it can be.
/// </para>
/// </remarks>
internal static class SourceRevision
{
    private static readonly CSharpParseOptions Options =
        new(LanguageVersion.Preview, DocumentationMode.None, SourceCodeKind.Regular);

    internal static SyntaxTree Parse(string text) => CSharpSyntaxTree.ParseText(text, Options);

    /// <summary>
    /// Every type the revision declares, keyed on fully-qualified name plus arity.
    /// </summary>
    internal static IReadOnlyDictionary<string, DeclaredType> DeclaredTypes(string text)
    {
        var root = Parse(text).GetRoot();
        var types = new Dictionary<string, DeclaredType>(StringComparer.Ordinal);

        foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            var name = QualifiedName(declaration);
            var read = Read(declaration, name);

            // A partial type declared twice in one file is one type: the members merge and the
            // headers concatenate in source order, which is deterministic for a given file.
            types[name] = types.TryGetValue(name, out var existing) ? Merge(existing, read) : read;
        }

        return types;
    }

    private static DeclaredType Merge(DeclaredType first, DeclaredType second)
    {
        var members = new Dictionary<MemberKey, DeclaredMember>(first.Members);

        foreach (var (key, member) in second.Members)
        {
            members[key] = member;
        }

        return first with { Header = first.Header + "\u0001" + second.Header, Members = members };
    }

    private static DeclaredType Read(BaseTypeDeclarationSyntax declaration, string name)
    {
        var members = new Dictionary<MemberKey, DeclaredMember>();

        foreach (var (key, member) in MembersOf(declaration))
        {
            members[key] = member;
        }

        return new DeclaredType(name, Header(declaration), members);
    }

    /// <summary>
    /// The type's own declaration with its members removed: modifiers, attributes, name, type
    /// parameters, base list and constraints.
    /// </summary>
    private static string Header(BaseTypeDeclarationSyntax declaration)
    {
        var tokens = declaration switch
        {
            TypeDeclarationSyntax type => Canonicaliser.Of(
                [
                    .. type.AttributeLists,
                    .. type.Modifiers.Select(modifier => (SyntaxNodeOrToken)modifier),
                    type.Keyword,
                    type.Identifier,
                    .. type.TypeParameterList is null ? Array.Empty<SyntaxNodeOrToken>() : [type.TypeParameterList],
                    .. type.ParameterList is null ? Array.Empty<SyntaxNodeOrToken>() : [type.ParameterList],
                    .. type.BaseList is null ? Array.Empty<SyntaxNodeOrToken>() : [type.BaseList],
                    .. type.ConstraintClauses,
                ]),
            EnumDeclarationSyntax @enum => Canonicaliser.Of(
                [
                    .. @enum.AttributeLists,
                    .. @enum.Modifiers.Select(modifier => (SyntaxNodeOrToken)modifier),
                    @enum.Identifier,
                    .. @enum.BaseList is null ? Array.Empty<SyntaxNodeOrToken>() : [@enum.BaseList],
                ]),
            _ => Canonicaliser.Of([declaration]),
        };

        return tokens;
    }

    private static IEnumerable<(MemberKey Key, DeclaredMember Member)> MembersOf(
        BaseTypeDeclarationSyntax declaration)
    {
        foreach (var member in Members(declaration))
        {
            // A nested type is a type in its own right, keyed on its own qualified name.
            if (member is BaseTypeDeclarationSyntax)
            {
                continue;
            }

            foreach (var declared in MemberDeclarations.Read(member))
            {
                yield return (declared.Key, declared);
            }
        }
    }

    /// <summary>
    /// The members as the tree holds them, never copied into a new list. Rebuilding a
    /// <see cref="SyntaxList{T}"/> from an enum's members reparents them onto a synthetic node
    /// with no source text behind it, and asking such a node for its location throws — which it
    /// does for every enum with more than one member, because a one-element list stores the
    /// node itself and needs no synthetic parent.
    /// </summary>
    private static IEnumerable<MemberDeclarationSyntax> Members(BaseTypeDeclarationSyntax declaration) =>
        declaration switch
        {
            TypeDeclarationSyntax type => type.Members,
            EnumDeclarationSyntax @enum => @enum.Members,
            _ => [],
        };

    /// <summary>
    /// The metadata spelling of the type's name: namespaces joined with <c>.</c>, nesting with
    /// <c>+</c>, and each level carrying its own arity after a backtick.
    /// </summary>
    private static string QualifiedName(BaseTypeDeclarationSyntax declaration)
    {
        var nested = new List<string>();

        for (SyntaxNode? node = declaration; node is not null; node = node.Parent)
        {
            var segment = node switch
            {
                TypeDeclarationSyntax type => type.Identifier.Text + Arity(type.TypeParameterList),
                EnumDeclarationSyntax @enum => @enum.Identifier.Text,
                _ => null,
            };

            if (segment is not null)
            {
                nested.Insert(0, segment);
            }
        }

        var containingNamespace = Namespace(declaration);
        var name = string.Join('+', nested);

        return containingNamespace.Length == 0 ? name : containingNamespace + "." + name;
    }

    private static string Arity(TypeParameterListSyntax? typeParameters) =>
        typeParameters is null or { Parameters.Count: 0 }
            ? string.Empty
            : "`" + typeParameters.Parameters.Count;

    private static string Namespace(SyntaxNode declaration)
    {
        var parts = new List<string>();

        for (var node = declaration; node is not null; node = node.Parent)
        {
            if (node is BaseNamespaceDeclarationSyntax @namespace)
            {
                parts.Insert(0, @namespace.Name.ToString());
            }
        }

        return string.Join('.', parts);
    }
}

/// <summary>
/// One type as one revision of one file declares it: a hash of its header, and a hash per
/// member.
/// </summary>
/// <param name="Name">The type's fully-qualified name, with its arity.</param>
/// <param name="Header">
/// Modifiers, attributes, base list and type parameters. Its own unit, because a change there
/// is whole-type widening — a test class gaining an attribute that makes its methods
/// discoverable is invisible to any member-level comparison.
/// </param>
/// <param name="Members">Each member the type declares, by key.</param>
internal sealed record DeclaredType(
    string Name,
    string Header,
    IReadOnlyDictionary<MemberKey, DeclaredMember> Members);

/// <summary>
/// One member as one revision of one file declares it.
/// </summary>
/// <param name="Key">The member's key within its type.</param>
/// <param name="Declaration">The declaration's canonical text, which is what two revisions compare.</param>
/// <param name="IsCompileTimeConstant">
/// True for a <c>const</c>, an enum member, or a declaration carrying a parameter default.
/// </param>
/// <param name="Span">
/// Where the declaration sits in its file, one-based, as debug symbols spell it. The join
/// matches a source declaration to the IL methods whose sequence points fall inside it.
/// </param>
internal sealed record DeclaredMember(
    MemberKey Key,
    string Declaration,
    bool IsCompileTimeConstant,
    DeclarationSpan Span);
