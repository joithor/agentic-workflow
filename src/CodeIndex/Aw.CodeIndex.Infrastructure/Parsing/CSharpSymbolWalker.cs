using System.Runtime.CompilerServices;
using Aw.CodeIndex.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SymbolKind = Aw.CodeIndex.Core.Model.SymbolKind;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>
/// Walks one file's syntax tree and collects its symbols and call sites (spec §5.3). Every step of the walk checks the
/// depth cap, the stack and the cancellation (CI12); a limit ends the file with a <see cref="ParseLimitExceededException"/>
/// that the parser turns into a skip. This part holds the walk and the type and member declarations; the properties and
/// the call sites are in the other parts.
/// </summary>
internal sealed partial class CSharpSymbolWalker(SourceText text, ParseLimits limits, CancellationToken cancellationToken) : CSharpSyntaxWalker
{
    private readonly List<SymbolDraft> _drafts = [];
    private readonly Stack<HashSet<string>> _valueMembers = new();      // per enclosing type
    private readonly Stack<HashSet<string>> _methodNames = new();       // per enclosing type
    private HashSet<string> _localFunctions = [];
    private string _container = string.Empty;
    private SymbolDraft? _owner;
    private bool _containerVisible = true;
    private bool _inInterface;
    private int _depth;

    public IReadOnlyList<ParsedSymbol> Run(SyntaxNode root)
    {
        Visit(root);
        return [.. _drafts.Select(d => d.ToSymbol())];
    }

    public override void Visit(SyntaxNode? node)
    {
        if (node is null)
        {
            return;
        }

        if (++_depth > limits.MaxDepth)
        {
            throw new ParseLimitExceededException(SkipReasons.TooDeep);
        }

        RuntimeHelpers.EnsureSufficientExecutionStack();
        cancellationToken.ThrowIfCancellationRequested();
        base.Visit(node);
        _depth--;
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node) => InNamespace(node);

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node) => InNamespace(node);

    public override void VisitClassDeclaration(ClassDeclarationSyntax node) => InType(node, SymbolKind.Class);

    public override void VisitStructDeclaration(StructDeclarationSyntax node) => InType(node, SymbolKind.Struct);

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) => InType(node, SymbolKind.Interface);

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node) => InType(node, SymbolKind.Record);

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node) => InType(node, SymbolKind.Enum);

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node) =>
        InMember(node, new MemberShape { Name = node.Identifier.ValueText, Kind = SymbolKind.Method, Parameters = node.ParameterList, Attributes = node.AttributeLists, Modifiers = node.Modifiers, HeadEnd = DeclarationHeads.EndOfHead(node.ParameterList, node.ConstraintClauses) });

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node) =>
        InMember(node, new MemberShape { Name = node.Identifier.ValueText, Kind = SymbolKind.Constructor, Parameters = node.ParameterList, Attributes = node.AttributeLists, Modifiers = node.Modifiers, HeadEnd = node.ParameterList.Span.End });

    public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node) =>
        InMember(node, new MemberShape { Name = "~" + node.Identifier.ValueText, Kind = SymbolKind.Method, Parameters = node.ParameterList, Attributes = node.AttributeLists, Modifiers = node.Modifiers, HeadEnd = node.ParameterList.Span.End });

    public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node) =>
        InMember(node, new MemberShape { Name = "operator " + node.OperatorToken.Text, Kind = SymbolKind.Method, Parameters = node.ParameterList, Attributes = node.AttributeLists, Modifiers = node.Modifiers, HeadEnd = node.ParameterList.Span.End });

    public override void VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node) =>
        InMember(node, new MemberShape { Name = "operator " + SyntaxText.Normalize(node.Type.ToString()), Kind = SymbolKind.Method, Parameters = node.ParameterList, Attributes = node.AttributeLists, Modifiers = node.Modifiers, HeadEnd = node.ParameterList.Span.End });

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var name = node.Identifier.ValueText;
        var head = TextSpan.FromBounds(DeclarationHeads.HeadStart(node, node.AttributeLists), DeclarationHeads.EndOfHead(node.ParameterList, node.ConstraintClauses));
        var draft = Declare(node, new Declaration
        {
            Name = name,
            Qualified = DeclarationHeads.Join(_container, name),
            Kind = SymbolKind.LocalFunction,
            Arity = DeclarationHeads.ParameterCount(node.ParameterList),
            ParameterTypes = DeclarationHeads.ParameterTypes(node.ParameterList),
            Exported = false,
            Head = head,
            Callable = true,
        });
        Enter(draft, node, _localFunctions);
    }

    private void InNamespace(BaseNamespaceDeclarationSyntax node)
    {
        var saved = _container;
        _container = DeclarationHeads.Join(_container, string.Concat(node.Name.DescendantTokens().Select(t => t.ValueText)));
        foreach (var member in node.Members)
        {
            Visit(member);
        }

        _container = saved;
    }

    private void InType(BaseTypeDeclarationSyntax type, SymbolKind kind)
    {
        var name = type.Identifier.ValueText;
        var exported = _containerVisible && DeclarationHeads.IsExported(type.Modifiers, _inInterface);
        var draft = Declare(type, new Declaration
        {
            Name = name,
            Qualified = DeclarationHeads.Join(_container, name),
            Kind = kind,
            Arity = (type as TypeDeclarationSyntax)?.TypeParameterList?.Parameters.Count ?? 0,
            Exported = exported,
            Head = TextSpan.FromBounds(DeclarationHeads.HeadStart(type, type.AttributeLists), DeclarationHeads.TypeHeadEnd(type)),
            Callable = false,
        });
        foreach (var site in CallSiteReader.Inherits(type.BaseList))
        {
            AddSite(draft, site);
        }

        var saved = Save();
        (_container, _owner, _containerVisible, _inInterface, _localFunctions) = (draft.Qualified, draft, exported, kind == SymbolKind.Interface, []);
        _valueMembers.Push(DeclarationHeads.ValueMembers(type));
        _methodNames.Push(DeclarationHeads.MethodNames(type));
        foreach (var child in type.ChildNodes())
        {
            Visit(child);
        }

        _ = _methodNames.Pop();
        _ = _valueMembers.Pop();
        Restore(saved);
    }

    private void InMember(SyntaxNode node, MemberShape shape)
    {
        var exported = _containerVisible && DeclarationHeads.IsExported(shape.Modifiers, _inInterface);
        var draft = Declare(node, new Declaration
        {
            Name = shape.Name,
            Qualified = DeclarationHeads.Join(_container, shape.Name),
            Kind = shape.Kind,
            Arity = DeclarationHeads.ParameterCount(shape.Parameters),
            ParameterTypes = DeclarationHeads.ParameterTypes(shape.Parameters),
            Exported = exported,
            Head = TextSpan.FromBounds(DeclarationHeads.HeadStart(node, shape.Attributes), shape.HeadEnd),
            Callable = true,
        });
        Enter(draft, node, DeclarationHeads.LocalFunctionNames(node));
    }

    private void Enter(SymbolDraft draft, SyntaxNode node, HashSet<string> localFunctions)
    {
        var saved = Save();
        (_container, _owner, _inInterface, _localFunctions) = (draft.Qualified, draft, false, localFunctions);
        foreach (var child in node.ChildNodes())
        {
            Visit(child);
        }

        Restore(saved);
    }

    private SymbolDraft Declare(SyntaxNode node, Declaration declaration)
    {
        if (_drafts.Count >= limits.MaxSymbols)
        {
            throw new ParseLimitExceededException(SkipReasons.TooManySymbols);
        }

        var head = declaration.Head;
        var draft = new SymbolDraft
        {
            Name = declaration.Name,
            Qualified = declaration.Qualified,
            Kind = declaration.Kind,
            Arity = declaration.Arity,
            ParameterTypesText = declaration.ParameterTypes,
            StartLine = LineOf(head.Start),
            EndLine = LineOf(Math.Max(node.Span.End - 1, node.SpanStart)),
            Exported = declaration.Exported,
            Signature = SyntaxText.Normalize(text.ToString(head)),
            Measure = SyntaxMeasures.Measure(node, declaration.Callable),
            Body = declaration.Callable ? text.ToString(node.Span) : null,
        };
        _drafts.Add(draft);
        return draft;
    }

    private int LineOf(int position) => text.Lines.GetLineFromPosition(position).LineNumber + 1;

    private Scope Save() => new(_container, _owner, _containerVisible, _inInterface, _localFunctions);

    private void Restore(Scope saved) => (_container, _owner, _containerVisible, _inInterface, _localFunctions) = (saved.Container, saved.Owner, saved.Visible, saved.InInterface, saved.Locals);

    private sealed record Scope(string Container, SymbolDraft? Owner, bool Visible, bool InInterface, HashSet<string> Locals);

    private sealed record MemberShape
    {
        public required string Name { get; init; }

        public required SymbolKind Kind { get; init; }

        public required BaseParameterListSyntax Parameters { get; init; }

        public required SyntaxList<AttributeListSyntax> Attributes { get; init; }

        public required SyntaxTokenList Modifiers { get; init; }

        public required int HeadEnd { get; init; }
    }
}
