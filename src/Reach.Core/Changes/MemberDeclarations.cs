using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Reach.Changes;

/// <summary>
/// Reads one member declaration into the members it declares, each with the key a revision is
/// compared on. Usually one, but <c>int a = 1, b = 2;</c> declares two, and a nested type or a
/// member the parser only recovered declares none.
/// </summary>
/// <remarks>
/// The member kinds follow Roslyn's own families: method-like declarations, property-like
/// declarations, field-like declarations, and the two that stand alone (a delegate and an enum
/// member).
/// </remarks>
internal static class MemberDeclarations
{
    internal static IEnumerable<DeclaredMember> Read(MemberDeclarationSyntax member) =>
        Describe(member).Select(described => new DeclaredMember(
            described.Key,
            Canonicaliser.Of([described.Node]),
            described.IsConstant,
            SpanOf(described.Node)));

    private static IEnumerable<Described> Describe(MemberDeclarationSyntax member) =>
        member switch
        {
            BaseMethodDeclarationSyntax method => MethodLike(method),
            BasePropertyDeclarationSyntax property => PropertyLike(property),
            BaseFieldDeclarationSyntax field => FieldLike(field),
            DelegateDeclarationSyntax @delegate =>
            [
                new(
                    new MemberKey(
                        MemberKind.Delegate,
                        @delegate.Identifier.Text,
                        @delegate.TypeParameterList?.Parameters.Count ?? 0,
                        ParameterTypes(@delegate.ParameterList)),
                    @delegate,
                    HasDefault(@delegate.ParameterList)),
            ],

            // An enum member's value is a compile-time constant and is baked into every
            // consumer that names it.
            EnumMemberDeclarationSyntax enumMember =>
            [
                new(new MemberKey(MemberKind.EnumMember, enumMember.Identifier.Text, 0, []), enumMember, true),
            ],

            // Nested types are walked as types in their own right, and anything else (an
            // incomplete member the parser recovered, for one) declares nothing to key on.
            _ => [],
        };

    private static IEnumerable<Described> MethodLike(BaseMethodDeclarationSyntax member) =>
        member switch
        {
            MethodDeclarationSyntax method =>
            [
                new(
                    new MemberKey(
                        MemberKind.Method,
                        method.Identifier.Text,
                        method.TypeParameterList?.Parameters.Count ?? 0,
                        ParameterTypes(method.ParameterList)),
                    method,
                    HasDefault(method.ParameterList)),
            ],
            ConstructorDeclarationSyntax constructor =>
            [
                new(
                    new MemberKey(MemberKind.Constructor, ".ctor", 0, ParameterTypes(constructor.ParameterList)),
                    constructor,
                    HasDefault(constructor.ParameterList)),
            ],
            DestructorDeclarationSyntax destructor =>
            [
                new(new MemberKey(MemberKind.Destructor, "Finalize", 0, []), destructor, false),
            ],
            OperatorDeclarationSyntax @operator =>
            [
                new(
                    new MemberKey(
                        MemberKind.Operator,
                        "op" + @operator.OperatorToken.Text,
                        0,
                        ParameterTypes(@operator.ParameterList)),
                    @operator,
                    false),
            ],
            ConversionOperatorDeclarationSyntax conversion =>
            [
                new(
                    new MemberKey(
                        MemberKind.ConversionOperator,
                        "op_" + conversion.ImplicitOrExplicitKeyword.Text + "_" + conversion.Type.ToString(),
                        0,
                        ParameterTypes(conversion.ParameterList)),
                    conversion,
                    false),
            ],
            _ => [],
        };

    private static IEnumerable<Described> PropertyLike(BasePropertyDeclarationSyntax member) =>
        member switch
        {
            IndexerDeclarationSyntax indexer =>
            [
                new(
                    new MemberKey(MemberKind.Indexer, "this[]", 0, ParameterTypes(indexer.ParameterList)),
                    indexer,
                    HasDefault(indexer.ParameterList)),
            ],
            PropertyDeclarationSyntax property =>
            [
                new(new MemberKey(MemberKind.Property, property.Identifier.Text, 0, []), property, false),
            ],
            EventDeclarationSyntax @event =>
            [
                new(new MemberKey(MemberKind.Event, @event.Identifier.Text, 0, []), @event, false),
            ],
            _ => [],
        };

    /// <summary>
    /// <c>int a = 1, b = 2;</c> is two members, and one of them can change alone. An event
    /// field keys every variable on the whole declaration; a field keys each on its own.
    /// </summary>
    private static IEnumerable<Described> FieldLike(BaseFieldDeclarationSyntax member) =>
        member switch
        {
            EventFieldDeclarationSyntax eventField => eventField.Declaration.Variables.Select(variable =>
                new Described(new MemberKey(MemberKind.Event, variable.Identifier.Text, 0, []), eventField, false)),
            FieldDeclarationSyntax field => Variables(field, field.Modifiers.Any(SyntaxKind.ConstKeyword)),
            _ => [],
        };

    private static IEnumerable<Described> Variables(FieldDeclarationSyntax field, bool isConst) =>
        field.Declaration.Variables.Select(variable =>
            new Described(new MemberKey(MemberKind.Field, variable.Identifier.Text, 0, []), variable, isConst));

    /// <summary>
    /// Roslyn counts lines and columns from zero; debug symbols count from one. Converted here
    /// so the join compares two things measured the same way.
    /// </summary>
    private static DeclarationSpan SpanOf(SyntaxNode node)
    {
        var span = node.GetLocation().GetLineSpan();

        return new DeclarationSpan(
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1,
            span.EndLinePosition.Character + 1);
    }

    /// <summary>
    /// The parameter's type <em>and</em> its passing mode. <c>ref</c>, <c>out</c> and <c>in</c>
    /// sit on the parameter rather than on the type, so without them <c>M(int)</c> and
    /// <c>M(ref int)</c> are one key — and one of two genuinely different overloads becomes
    /// invisible to change detection.
    /// </summary>
    private static IReadOnlyList<string> ParameterTypes(BaseParameterListSyntax? parameters) =>
        parameters is null
            ? []
            : [.. parameters.Parameters.Select(parameter => Mode(parameter) + TypeOf(parameter))];

    /// <summary>
    /// As written, with runs of whitespace collapsed — <em>not</em> through the canonicaliser.
    /// A member key is a display form as well as a key: it reaches the report's forward change
    /// list, and the canonicaliser's U+0001 token separator has no business in a product
    /// surface.
    /// </summary>
    private static string TypeOf(ParameterSyntax parameter) =>
        parameter.Type is null
            ? string.Empty
            : string.Join(' ', parameter.Type.ToString().Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries));

    private static string Mode(ParameterSyntax parameter)
    {
        var mode = parameter.Modifiers.FirstOrDefault(IsPassingMode);

        return mode.IsKind(SyntaxKind.None) ? string.Empty : mode.Text + " ";
    }

    private static bool IsPassingMode(SyntaxToken modifier) =>
        modifier.IsKind(SyntaxKind.RefKeyword)
        || modifier.IsKind(SyntaxKind.OutKeyword)
        || modifier.IsKind(SyntaxKind.InKeyword);

    private static bool HasDefault(BaseParameterListSyntax? parameters) =>
        parameters is not null && parameters.Parameters.Any(parameter => parameter.Default is not null);

    /// <summary>
    /// One member a declaration declares: its key, the node whose text is compared, and whether
    /// its value is baked into consumers.
    /// </summary>
    private readonly record struct Described(MemberKey Key, SyntaxNode Node, bool IsConstant);
}
