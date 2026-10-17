using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1002: every null-forgiving '!' carries a reason (spec §4b.3): a '// why: &lt;reason&gt;' comment on the
/// line of the '!', or in the comment lines directly above the statement or member that holds it (or between its
/// attributes and the declaration), with no blank line between. The text after 'why:' must not be empty.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NullForgivingReasonAnalyzer : DiagnosticAnalyzer
{
    private const string Marker = "// why:";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [AwDiagnostics.NullForgivingReason];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SuppressNullableWarningExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = (PostfixUnaryExpressionSyntax)context.Node;
        if (HasReasonOnLine(node, context.CancellationToken) || HasReasonAbove(node))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(AwDiagnostics.NullForgivingReason, node.OperatorToken.GetLocation(), node.ToString()));
    }

    // A '// why:' comment anywhere on the line of the '!' (normally trailing).
    private static bool HasReasonOnLine(PostfixUnaryExpressionSyntax node, CancellationToken cancellationToken)
    {
        var tree = node.SyntaxTree;
        var line = tree.GetText(cancellationToken).Lines.GetLineFromPosition(node.OperatorToken.SpanStart);
        var token = tree.GetRoot(cancellationToken).FindToken(line.Start);
        while (token.RawKind != 0 && token.SpanStart <= line.End)
        {
            if (token.LeadingTrivia.Concat(token.TrailingTrivia).Any(t => OnLine(t, line) && IsReason(t)))
            {
                return true;
            }

            token = token.GetNextToken();
        }

        return false;
    }

    // A '// why:' comment in the leading trivia of the statement or member that holds the '!', before its attribute
    // lists or between them and the declaration.
    private static bool HasReasonAbove(SyntaxNode node)
    {
        var holder = node.AncestorsAndSelf().FirstOrDefault(n => n is StatementSyntax or MemberDeclarationSyntax);
        if (holder is null)
        {
            return false;
        }

        var attributes = holder switch
        {
            MemberDeclarationSyntax member => member.AttributeLists,
            StatementSyntax statement => statement.AttributeLists,
            _ => default,
        };
        return HasReasonDirectlyAbove(holder.GetLeadingTrivia())
            || (attributes.Count > 0 && HasReasonDirectlyAbove(attributes.Last().GetLastToken().GetNextToken().LeadingTrivia));
    }

    // A reason in the comment lines right before the token: walking up, a blank line ends the search.
    private static bool HasReasonDirectlyAbove(SyntaxTriviaList trivia)
    {
        var lineEnded = false;
        for (var i = trivia.Count - 1; i >= 0; i--)
        {
            var t = trivia[i];
            if (t.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                if (lineEnded)
                {
                    return false;   // two line ends with only whitespace between: a blank line
                }

                lineEnded = true;
            }
            else if (!t.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                if (IsReason(t))
                {
                    return true;
                }

                lineEnded = false;
            }
        }

        return false;
    }

    private static bool OnLine(SyntaxTrivia trivia, Microsoft.CodeAnalysis.Text.TextLine line) =>
        trivia.SpanStart >= line.Start && trivia.SpanStart <= line.End;

    private static bool IsReason(SyntaxTrivia trivia)
    {
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
        {
            return false;
        }

        var text = trivia.ToString();
        return text.StartsWith(Marker, StringComparison.Ordinal) && text.Substring(Marker.Length).Trim().Length > 0;
    }
}
