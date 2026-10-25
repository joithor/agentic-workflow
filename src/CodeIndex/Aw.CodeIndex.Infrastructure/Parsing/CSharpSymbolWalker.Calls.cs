using Aw.CodeIndex.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SymbolKind = Aw.CodeIndex.Core.Model.SymbolKind;

namespace Aw.CodeIndex.Infrastructure.Parsing;

// The call-site and named-lambda part of the walker.
internal sealed partial class CSharpSymbolWalker
{
    private int _callSites;

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        AddOwned(CallSiteReader.Invocation(node, IsValueMember));
        base.VisitInvocationExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        AddOwned(CallSiteReader.Creation(node));
        base.VisitObjectCreationExpression(node);
    }

    public override void VisitArgument(ArgumentSyntax node)
    {
        AddMethodGroup(node.Expression);
        base.VisitArgument(node);
    }

    public override void VisitEqualsValueClause(EqualsValueClauseSyntax node)
    {
        AddMethodGroup(node.Value);
        base.VisitEqualsValueClause(node);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.SimpleAssignmentExpression) || node.IsKind(SyntaxKind.AddAssignmentExpression) || node.IsKind(SyntaxKind.SubtractAssignmentExpression))
        {
            AddMethodGroup(node.Right);
        }

        base.VisitAssignmentExpression(node);
    }

    // A local variable or field that holds a lambda is a symbol of its own: its calls belong to it.
    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        if (node.Initializer?.Value is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax)
        {
            InNamedLambda(node);
            return;
        }

        base.VisitVariableDeclarator(node);
    }

    private void InNamedLambda(VariableDeclaratorSyntax declarator)
    {
        var name = declarator.Identifier.ValueText;
        var (arity, types, headEnd) = LambdaHead(declarator);
        var declaration = declarator.Parent as VariableDeclarationSyntax;
        var start = declaration?.SpanStart ?? declarator.SpanStart;
        var draft = Declare(declarator, new Declaration
        {
            Name = name,
            Qualified = DeclarationHeads.Join(_container, name),
            Kind = SymbolKind.Lambda,
            Arity = arity,
            ParameterTypes = types,
            Exported = false,
            Head = TextSpan.FromBounds(start, declarator.Span.End),
            Callable = true,
        });
        var head = SyntaxText.Normalize(text.ToString(TextSpan.FromBounds(declarator.SpanStart, headEnd)));
        draft.Signature = declaration is null ? head : SyntaxText.Normalize(declaration.Type.ToString()) + " " + head;
        Enter(draft, declarator, _localFunctions);
    }

    // Parameter count, parameter types and where the head ends: a lambda with a block body is cut at its arrow, one with an expression body keeps it.
    private static (int Arity, string Types, int HeadEnd) LambdaHead(VariableDeclaratorSyntax declarator)
    {
        var whole = declarator.Span.End;
        return declarator.Initializer?.Value switch
        {
            SimpleLambdaExpressionSyntax simple => (1, string.Empty, simple.Block is null ? whole : simple.ArrowToken.Span.End),
            ParenthesizedLambdaExpressionSyntax parenthesized => (DeclarationHeads.ParameterCount(parenthesized.ParameterList), DeclarationHeads.ParameterTypes(parenthesized.ParameterList), parenthesized.Block is null ? whole : parenthesized.ArrowToken.Span.End),
            AnonymousMethodExpressionSyntax method => AnonymousMethodHead(method, whole),
            _ => (0, string.Empty, whole),
        };
    }

    private static (int Arity, string Types, int HeadEnd) AnonymousMethodHead(AnonymousMethodExpressionSyntax method, int whole)
    {
        var parameters = method.ParameterList;
        var end = method.Block is null ? whole : parameters?.Span.End ?? method.DelegateKeyword.Span.End;
        return parameters is null
            ? (0, string.Empty, end)
            : (DeclarationHeads.ParameterCount(parameters), DeclarationHeads.ParameterTypes(parameters), end);
    }

    private bool IsValueMember(string name) => _valueMembers.Any(members => members.Contains(name));

    // A bare name that is a local function of the enclosing member or a method of the enclosing type, used as a value: a method group conversion.
    private void AddMethodGroup(ExpressionSyntax? expression)
    {
        if (expression is IdentifierNameSyntax name
            && (_localFunctions.Contains(name.Identifier.ValueText) || (_methodNames.Count > 0 && _methodNames.Peek().Contains(name.Identifier.ValueText))))
        {
            AddOwned(CallSiteReader.MethodGroup(name));
        }
    }

    private void AddOwned(RawSite? site)
    {
        if (site is not null && _owner is not null)
        {
            AddSite(_owner, site);
        }
    }

    private void AddSite(SymbolDraft owner, RawSite site)
    {
        if (++_callSites > limits.MaxCallSites)
        {
            throw new ParseLimitExceededException(SkipReasons.TooManyCallSites);
        }

        owner.CallSites.Add(new CallSite(site.Kind, site.Name, site.Arity, site.Receiver, LineOf(site.At.SpanStart)));
    }
}
