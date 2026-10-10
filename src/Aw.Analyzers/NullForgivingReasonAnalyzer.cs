using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aw.Analyzers;

/// <summary>
/// AW1002: every null-forgiving '!' carries a reason (spec §4b.3): a '// why: &lt;reason&gt;' comment on the
/// line of the '!', or in the comment lines directly above the statement or member that holds it. The text
/// after 'why:' must not be empty.
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
        if (HasReasonOnLine(node) || HasReasonAbove(node))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(AwDiagnostics.NullForgivingReason, node.OperatorToken.GetLocation(), node.ToString()));
    }

    // A '// why:' comment anywhere on the line of the '!' (normally trailing).
    private static bool HasReasonOnLine(PostfixUnaryExpressionSyntax node)
    {
        var tree = node.SyntaxTree;
        var line = tree.GetText().Lines[node.OperatorToken.GetLocation().GetLineSpan().StartLinePosition.Line];
        var token = tree.GetRoot().FindToken(line.Start);
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

    // A '// why:' comment in the leading trivia of the statement or member that holds the '!'.
    private static bool HasReasonAbove(SyntaxNode node)
    {
        var holder = node.AncestorsAndSelf().FirstOrDefault(n => n is StatementSyntax or MemberDeclarationSyntax);
        return holder is not null && holder.GetLeadingTrivia().Any(IsReason);
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
