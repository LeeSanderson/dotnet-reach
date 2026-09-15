using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Reach.Changes;

/// <summary>
/// Reduces a declaration to the text that decides what it compiles to, discarding the text
/// that does not.
/// </summary>
/// <remarks>
/// <para>
/// This is what preserves <em>comment and formatting churn must not select</em>. The unit of
/// comparison is the whole declaration — modifiers, attributes, signature, parameter defaults,
/// initializer and body — not just the body, which closes the declaring-side gap in one move:
/// <c>const</c> values, field initializers, enum members, attribute arguments and default
/// parameter values all sit inside the declaration. It also settles the expensive case:
/// adding <c>[Fact]</c> to an existing method registers as a change, so "new since the
/// baseline" stays derivable from the changed set with no separate mechanism.
/// </para>
/// <para>
/// Because the property now has to hold over a much larger syntactic surface than a body hash,
/// it gets its own tests rather than being assumed.
/// </para>
/// </remarks>
internal static class Canonicaliser
{
    /// <summary>Separates tokens, so that <c>ab</c> and <c>a b</c> can never canonicalise alike.</summary>
    private const char Separator = '';

    internal static string Of(IReadOnlyList<SyntaxNodeOrToken> parts)
    {
        var builder = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.IsToken)
            {
                Append(builder, part.AsToken());
            }
            else
            {
                foreach (var token in part.AsNode()!.DescendantTokens())
                {
                    Append(builder, token);
                }
            }
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, SyntaxToken token)
    {
        AppendSignificant(builder, token.LeadingTrivia);
        builder.Append(token.Text).Append(Separator);
        AppendSignificant(builder, token.TrailingTrivia);
    }

    /// <summary>
    /// Almost all trivia is discarded. Two kinds are not, because they change what compiles
    /// while leaving the token stream identical: the text inside an inactive <c>#if</c>, and
    /// the conditional directives that decide which branch is inactive.
    /// </summary>
    private static void AppendSignificant(StringBuilder builder, SyntaxTriviaList trivia)
    {
        foreach (var item in trivia)
        {
            if (IsSignificant(item))
            {
                builder.Append(item.ToString()).Append(Separator);
            }
        }
    }

    private static bool IsSignificant(SyntaxTrivia trivia) => trivia.Kind() switch
    {
        // The branch the parser did not take. Invisible to the token stream, and a change to
        // it changes what another configuration compiles.
        SyntaxKind.DisabledTextTrivia => true,

        SyntaxKind.IfDirectiveTrivia
            or SyntaxKind.ElifDirectiveTrivia
            or SyntaxKind.ElseDirectiveTrivia
            or SyntaxKind.EndIfDirectiveTrivia
            or SyntaxKind.DefineDirectiveTrivia
            or SyntaxKind.UndefDirectiveTrivia => true,

        // #region, #pragma, #nullable, #line and every comment form are formatting or
        // tooling, not compilation.
        _ => false,
    };
}
