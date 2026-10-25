using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SymbolKind = Aw.CodeIndex.Core.Model.SymbolKind;

namespace Aw.CodeIndex.Infrastructure.Parsing;

// The property, indexer and event part of the walker: only an accessor with a body (or an expression-bodied property) is a symbol.
internal sealed partial class CSharpSymbolWalker
{
    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node) =>
        InProperty(node, new PropertyShape { Name = node.Identifier.ValueText, Attributes = node.AttributeLists, Modifiers = node.Modifiers, HeadEnd = node.Identifier.Span.End, Accessors = node.AccessorList, ExpressionBody = node.ExpressionBody });

    public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node) =>
        InProperty(node, new PropertyShape { Name = "this[]", Attributes = node.AttributeLists, Modifiers = node.Modifiers, HeadEnd = node.ParameterList.Span.End, Accessors = node.AccessorList, ExpressionBody = node.ExpressionBody });

    public override void VisitEventDeclaration(EventDeclarationSyntax node) =>
        InProperty(node, new PropertyShape { Name = node.Identifier.ValueText, Attributes = node.AttributeLists, Modifiers = node.Modifiers, HeadEnd = node.Identifier.Span.End, Accessors = node.AccessorList, ExpressionBody = null });

    private void InProperty(SyntaxNode node, PropertyShape shape)
    {
        var qualified = DeclarationHeads.Join(_container, shape.Name);
        var exported = _containerVisible && DeclarationHeads.IsExported(shape.Modifiers, _inInterface);
        var expressionDraft = shape.ExpressionBody is null
            ? null
            : DeclareAccessor(node, "get", qualified, new AccessorFacts(exported, 0, TextSpan.FromBounds(DeclarationHeads.HeadStart(node, shape.Attributes), shape.HeadEnd)));
        foreach (var child in node.ChildNodes())
        {
            if (child is AccessorListSyntax list)
            {
                InAccessors(list, qualified, exported);
            }
            else if (expressionDraft is not null && ReferenceEquals(child, shape.ExpressionBody))
            {
                Enter(expressionDraft, child, DeclarationHeads.LocalFunctionNames(child));
            }
            else
            {
                Visit(child);
            }
        }
    }

    private void InAccessors(AccessorListSyntax list, string propertyQualified, bool propertyExported)
    {
        foreach (var accessor in list.Accessors.Where(a => a.Body is not null || a.ExpressionBody is not null))
        {
            var keyword = accessor.Keyword.ValueText;
            var exported = accessor.Modifiers.Count == 0 ? propertyExported : propertyExported && DeclarationHeads.IsExported(accessor.Modifiers, inInterface: false);
            var head = TextSpan.FromBounds(DeclarationHeads.HeadStart(accessor, accessor.AttributeLists), accessor.Keyword.Span.End);
            var draft = DeclareAccessor(accessor, keyword, propertyQualified, new AccessorFacts(exported, keyword == "get" ? 0 : 1, head));
            Enter(draft, accessor, DeclarationHeads.LocalFunctionNames(accessor));
        }
    }

    private SymbolDraft DeclareAccessor(SyntaxNode node, string keyword, string propertyQualified, AccessorFacts facts) =>
        Declare(node, new Declaration
        {
            Name = keyword,
            Qualified = DeclarationHeads.Join(propertyQualified, keyword),
            Kind = SymbolKind.Accessor,
            Arity = facts.Arity,
            Exported = facts.Exported,
            Head = facts.Head,
            Callable = true,
        });

    private sealed record AccessorFacts(bool Exported, int Arity, TextSpan Head);

    private sealed record PropertyShape
    {
        public required string Name { get; init; }

        public required SyntaxList<AttributeListSyntax> Attributes { get; init; }

        public required SyntaxTokenList Modifiers { get; init; }

        public required int HeadEnd { get; init; }

        public required AccessorListSyntax? Accessors { get; init; }

        public required ArrowExpressionClauseSyntax? ExpressionBody { get; init; }
    }
}
