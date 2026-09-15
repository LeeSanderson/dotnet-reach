using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reach.Changes;

/// <summary>
/// One type as one revision of one file declares it: a hash of its header, and a hash per
/// member.
/// </summary>
/// <param name="Header">
/// Modifiers, attributes, base list and type parameters. Its own unit, because a change there
/// is whole-type widening — a test class gaining an attribute that makes its methods
/// discoverable is invisible to any member-level comparison.
/// </param>
internal sealed record DeclaredType(
    string Name,
    string Header,
    IReadOnlyDictionary<MemberKey, DeclaredMember> Members);

internal sealed record DeclaredMember(MemberKey Key, string Declaration, bool IsCompileTimeConstant);

/// <summary>
/// Reads one revision of one C# file into the types it declares. The only place in Reach that
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

    /// <summary>Every type the revision declares, keyed on fully-qualified name plus arity.</summary>
    internal static IReadOnlyDictionary<string, DeclaredType> DeclaredTypes(string text)
    {
        var root = Parse(text).GetRoot();
        var types = new Dictionary<string, DeclaredType>(StringComparer.Ordinal);

        foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            var name = QualifiedName(declaration);

            // A partial type declared twice in one file is one type: the members merge and the
            // headers concatenate in source order, which is deterministic for a given file.
            if (types.TryGetValue(name, out var existing))
            {
                types[name] = Merge(existing, Read(declaration, name));
            }
            else
            {
                types[name] = Read(declaration, name);
            }
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

        return first with { Header = first.Header + "" + second.Header, Members = members };
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

            foreach (var (key, node, isConstant) in Describe(member))
            {
                yield return (key, new DeclaredMember(key, Canonicaliser.Of([node]), isConstant));
            }
        }
    }

    private static SyntaxList<MemberDeclarationSyntax> Members(BaseTypeDeclarationSyntax declaration) =>
        declaration switch
        {
            TypeDeclarationSyntax type => type.Members,
            EnumDeclarationSyntax @enum => new SyntaxList<MemberDeclarationSyntax>(@enum.Members),
            _ => default,
        };

    private static IEnumerable<(MemberKey Key, SyntaxNode Node, bool IsConstant)> Describe(
        MemberDeclarationSyntax member)
    {
        switch (member)
        {
            case MethodDeclarationSyntax method:
                yield return (
                    new MemberKey(
                        MemberKind.Method,
                        method.Identifier.Text,
                        method.TypeParameterList?.Parameters.Count ?? 0,
                        ParameterTypes(method.ParameterList)),
                    method,
                    HasDefault(method.ParameterList));
                break;

            case ConstructorDeclarationSyntax constructor:
                yield return (
                    new MemberKey(MemberKind.Constructor, ".ctor", 0, ParameterTypes(constructor.ParameterList)),
                    constructor,
                    HasDefault(constructor.ParameterList));
                break;

            case DestructorDeclarationSyntax destructor:
                yield return (new MemberKey(MemberKind.Destructor, "Finalize", 0, []), destructor, false);
                break;

            case OperatorDeclarationSyntax @operator:
                yield return (
                    new MemberKey(
                        MemberKind.Operator,
                        "op" + @operator.OperatorToken.Text,
                        0,
                        ParameterTypes(@operator.ParameterList)),
                    @operator,
                    false);
                break;

            case ConversionOperatorDeclarationSyntax conversion:
                yield return (
                    new MemberKey(
                        MemberKind.ConversionOperator,
                        "op_" + conversion.ImplicitOrExplicitKeyword.Text + "_" + conversion.Type.ToString(),
                        0,
                        ParameterTypes(conversion.ParameterList)),
                    conversion,
                    false);
                break;

            case IndexerDeclarationSyntax indexer:
                yield return (
                    new MemberKey(MemberKind.Indexer, "this[]", 0, ParameterTypes(indexer.ParameterList)),
                    indexer,
                    HasDefault(indexer.ParameterList));
                break;

            case PropertyDeclarationSyntax property:
                yield return (
                    new MemberKey(MemberKind.Property, property.Identifier.Text, 0, []),
                    property,
                    false);
                break;

            case EventDeclarationSyntax @event:
                yield return (new MemberKey(MemberKind.Event, @event.Identifier.Text, 0, []), @event, false);
                break;

            case DelegateDeclarationSyntax @delegate:
                yield return (
                    new MemberKey(
                        MemberKind.Delegate,
                        @delegate.Identifier.Text,
                        @delegate.TypeParameterList?.Parameters.Count ?? 0,
                        ParameterTypes(@delegate.ParameterList)),
                    @delegate,
                    HasDefault(@delegate.ParameterList));
                break;

            case EnumMemberDeclarationSyntax enumMember:
                // An enum member's value is a compile-time constant and is baked into every
                // consumer that names it.
                yield return (
                    new MemberKey(MemberKind.EnumMember, enumMember.Identifier.Text, 0, []),
                    enumMember,
                    true);
                break;

            case EventFieldDeclarationSyntax eventField:
                foreach (var variable in eventField.Declaration.Variables)
                {
                    yield return (
                        new MemberKey(MemberKind.Event, variable.Identifier.Text, 0, []),
                        eventField,
                        false);
                }

                break;

            case FieldDeclarationSyntax field:
                // `int a = 1, b = 2;` is two members, and one of them can change alone.
                var isConst = field.Modifiers.Any(SyntaxKind.ConstKeyword);

                foreach (var variable in field.Declaration.Variables)
                {
                    yield return (
                        new MemberKey(MemberKind.Field, variable.Identifier.Text, 0, []),
                        variable,
                        isConst);
                }

                break;
        }
    }

    private static IReadOnlyList<string> ParameterTypes(BaseParameterListSyntax? parameters) =>
        parameters is null
            ? []
            : [.. parameters.Parameters.Select(parameter => Canonicaliser.Of([parameter.Type!]))];

    private static bool HasDefault(BaseParameterListSyntax? parameters) =>
        parameters is not null && parameters.Parameters.Any(parameter => parameter.Default is not null);

    /// <summary>
    /// The metadata spelling of the type's name: namespaces joined with <c>.</c>, nesting with
    /// <c>+</c>, and each level carrying its own arity after a backtick.
    /// </summary>
    private static string QualifiedName(BaseTypeDeclarationSyntax declaration)
    {
        var nested = new List<string>();

        for (SyntaxNode? node = declaration; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case TypeDeclarationSyntax type:
                    nested.Insert(0, type.Identifier.Text + Arity(type.TypeParameterList));
                    break;

                case EnumDeclarationSyntax @enum:
                    nested.Insert(0, @enum.Identifier.Text);
                    break;
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

        for (SyntaxNode? node = declaration; node is not null; node = node.Parent)
        {
            if (node is BaseNamespaceDeclarationSyntax @namespace)
            {
                parts.Insert(0, @namespace.Name.ToString());
            }
        }

        return string.Join('.', parts);
    }
}
