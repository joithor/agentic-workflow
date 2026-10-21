using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>What one declaration measures to: its normalized shape hash, its cyclomatic complexity and the token list MinHash reads (spec §5.3).</summary>
/// <param name="ShapeHash">16 hex digits of the hash of the node and token kinds, identifiers and literals blanked.</param>
/// <param name="Complexity">1 plus the decision nodes and short-circuit operators, not counting nested local functions and named lambdas (they are symbols of their own); 0 for a type.</param>
/// <param name="Tokens">The normalized tokens of a callable symbol (an identifier is 'id', a literal 'lit', anything else its text); empty for a type.</param>
internal sealed record SyntaxMeasure(string ShapeHash, int Complexity, IReadOnlyList<string> Tokens);

/// <summary>Measures declarations; every walk here is iterative, so a deep tree cannot overflow the stack.</summary>
internal static class SyntaxMeasures
{
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
            var mark = child.IsNode ? NodeMark(child.AsNode()!) : TokenMark(child.AsToken());
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

    private static int TokenMark(SyntaxToken token) => token.Kind() switch
    {
        SyntaxKind.IdentifierToken => IdentifierMark,
        SyntaxKind.NumericLiteralToken or SyntaxKind.CharacterLiteralToken or SyntaxKind.StringLiteralToken
            or SyntaxKind.Utf8StringLiteralToken or SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken
            or SyntaxKind.Utf8SingleLineRawStringLiteralToken or SyntaxKind.Utf8MultiLineRawStringLiteralToken
            or SyntaxKind.InterpolatedStringTextToken or SyntaxKind.TrueKeyword or SyntaxKind.FalseKeyword or SyntaxKind.NullKeyword => LiteralMark,
        _ => (int)token.Kind(),
    };

    private static int Complexity(SyntaxNode root)
    {
        var count = 1;
        foreach (var node in root.DescendantNodesAndSelf(descendIntoChildren: n => ReferenceEquals(n, root) || !IsSymbolOfItsOwn(n)))
        {
            if (node.Kind() is SyntaxKind.IfStatement or SyntaxKind.WhileStatement or SyntaxKind.DoStatement or SyntaxKind.ForStatement
                or SyntaxKind.ForEachStatement or SyntaxKind.ForEachVariableStatement or SyntaxKind.CaseSwitchLabel or SyntaxKind.CasePatternSwitchLabel
                or SyntaxKind.SwitchExpressionArm or SyntaxKind.CatchClause or SyntaxKind.ConditionalExpression
                or SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression or SyntaxKind.CoalesceAssignmentExpression)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsSymbolOfItsOwn(SyntaxNode node) =>
        node is LocalFunctionStatementSyntax or VariableDeclaratorSyntax { Initializer.Value: LambdaExpressionSyntax or AnonymousMethodExpressionSyntax };
}
