using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>Measures declarations; every walk here is iterative, so a deep tree cannot overflow the stack.</summary>
internal static class SyntaxMeasures
{
    private static readonly HashSet<SyntaxKind> _literalTokens =
    [
        SyntaxKind.NumericLiteralToken, SyntaxKind.CharacterLiteralToken, SyntaxKind.StringLiteralToken, SyntaxKind.Utf8StringLiteralToken,
        SyntaxKind.SingleLineRawStringLiteralToken, SyntaxKind.MultiLineRawStringLiteralToken, SyntaxKind.Utf8SingleLineRawStringLiteralToken,
        SyntaxKind.Utf8MultiLineRawStringLiteralToken, SyntaxKind.InterpolatedStringTextToken, SyntaxKind.TrueKeyword, SyntaxKind.FalseKeyword,
        SyntaxKind.NullKeyword,
    ];

    private static readonly HashSet<SyntaxKind> _decisions =
    [
        SyntaxKind.IfStatement, SyntaxKind.WhileStatement, SyntaxKind.DoStatement, SyntaxKind.ForStatement, SyntaxKind.ForEachStatement,
        SyntaxKind.ForEachVariableStatement, SyntaxKind.CaseSwitchLabel, SyntaxKind.CasePatternSwitchLabel, SyntaxKind.SwitchExpressionArm,
        SyntaxKind.CatchClause, SyntaxKind.ConditionalExpression, SyntaxKind.LogicalAndExpression, SyntaxKind.LogicalOrExpression,
        SyntaxKind.CoalesceExpression, SyntaxKind.CoalesceAssignmentExpression,
    ];

    private const int IdentifierMark = -1;
    private const int LiteralMark = -2;
    private const int LiteralNodeMark = -3;

    /// <summary>Measures a declaration.</summary>
    /// <param name="node">The declaration.</param>
    /// <param name="callable">True for a callable symbol (it gets tokens and a complexity), false for a type.</param>
    /// <returns>The measure.</returns>
    public static SyntaxMeasure Measure(SyntaxNode node, bool callable)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> buffer = stackalloc byte[4];
        var tokens = new List<string>();
        foreach (var child in node.DescendantNodesAndTokensAndSelf())
        {
            var mark = child.AsNode() is { } declared ? NodeMark(declared) : TokenMark(child.AsToken());
            BinaryPrimitives.WriteInt32LittleEndian(buffer, mark);
            hash.AppendData(buffer);
            if (callable && child.IsToken)
            {
                tokens.Add(mark switch { IdentifierMark => "id", LiteralMark => "lit", _ => child.AsToken().Text });
            }
        }

        return new SyntaxMeasure(Convert.ToHexStringLower(hash.GetHashAndReset().AsSpan(0, 8)), callable ? Complexity(node) : 0, tokens);
    }

    private static int NodeMark(SyntaxNode node) => SyntaxFacts.IsLiteralExpression(node.Kind()) ? LiteralNodeMark : (int)node.Kind();

    private static int TokenMark(SyntaxToken token)
    {
        var kind = token.Kind();
        if (kind == SyntaxKind.IdentifierToken)
        {
            return IdentifierMark;
        }

        return _literalTokens.Contains(kind) ? LiteralMark : (int)kind;
    }

    private static int Complexity(SyntaxNode root)
    {
        var count = 1;
        foreach (var node in root.DescendantNodesAndSelf(descendIntoChildren: n => ReferenceEquals(n, root) || !IsSymbolOfItsOwn(n)))
        {
            if (_decisions.Contains(node.Kind()))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsSymbolOfItsOwn(SyntaxNode node) =>
        node is LocalFunctionStatementSyntax or VariableDeclaratorSyntax { Initializer.Value: LambdaExpressionSyntax or AnonymousMethodExpressionSyntax };
}
