using System.Runtime.CompilerServices;
using Aw.CodeIndex.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>
/// Walks one file's syntax tree and collects its symbols and call sites (spec §5.3). The walk is recursive and guarded
/// (CI12): every step checks the depth cap, the stack and the cancellation, and a limit ends the file with a
/// <see cref="ParseLimitExceededException"/> that the parser turns into a skip.
/// </summary>
internal sealed class CSharpSymbolWalker
{
    private readonly SourceText _text;
    private readonly ParseLimits _limits;
    private readonly CancellationToken _cancellationToken;
    private readonly List<Draft> _drafts = [];
    private readonly Stack<HashSet<string>> _valueMembers = new();      // per enclosing type: properties, fields, events and primary-constructor parameters
    private readonly Stack<HashSet<string>> _methodNames = new();       // per enclosing type: its methods
    private HashSet<string> _localFunctions = [];
    private string _container = string.Empty;
    private Draft? _owner;
    private bool _containerVisible = true;
    private bool _inInterface;
    private int _callSites;

    public CSharpSymbolWalker(SourceText text, ParseLimits limits, CancellationToken cancellationToken)
    {
        _text = text;
        _limits = limits;
        _cancellationToken = cancellationToken;
    }

    public IReadOnlyList<ParsedSymbol> Run(SyntaxNode root)
    {
        Walk(root, 0);
        return [.. _drafts.Select(d => d.ToSymbol())];
    }

    private void Walk(SyntaxNode node, int depth)
    {
        if (depth > _limits.MaxDepth)
        {
            throw new ParseLimitExceededException(SkipReasons.TooDeep);
        }

        RuntimeHelpers.EnsureSufficientExecutionStack();
        _cancellationToken.ThrowIfCancellationRequested();

        switch (node)
        {
            case BaseNamespaceDeclarationSyntax @namespace:
                InNamespace(@namespace, depth);
                break;
            case EnumDeclarationSyntax @enum:
                InType(@enum, SymbolKind.Enum, 0, @enum.AttributeLists, @enum.Modifiers, @enum.Identifier, [], depth);
                break;
            case TypeDeclarationSyntax type:
                InType(type, TypeKind(type), type.TypeParameterList?.Parameters.Count ?? 0, type.AttributeLists, type.Modifiers, type.Identifier, type.ParameterList?.Parameters, depth);
                break;
            case MethodDeclarationSyntax method:
                InMember(method, method.Identifier.ValueText, SymbolKind.Method, method.ParameterList, method.AttributeLists, method.Modifiers, EndOfHead(method.ParameterList, method.ConstraintClauses), depth);
                break;
            case ConstructorDeclarationSyntax constructor:
                InMember(constructor, constructor.Identifier.ValueText, SymbolKind.Constructor, constructor.ParameterList, constructor.AttributeLists, constructor.Modifiers, constructor.ParameterList.Span.End, depth);
                break;
            case DestructorDeclarationSyntax destructor:
                InMember(destructor, "~" + destructor.Identifier.ValueText, SymbolKind.Method, destructor.ParameterList, destructor.AttributeLists, destructor.Modifiers, destructor.ParameterList.Span.End, depth);
                break;
            case OperatorDeclarationSyntax @operator:
                InMember(@operator, "operator " + @operator.OperatorToken.Text, SymbolKind.Method, @operator.ParameterList, @operator.AttributeLists, @operator.Modifiers, @operator.ParameterList.Span.End, depth);
                break;
            case ConversionOperatorDeclarationSyntax conversion:
                InMember(conversion, "operator " + SyntaxText.Normalize(conversion.Type.ToString()), SymbolKind.Method, conversion.ParameterList, conversion.AttributeLists, conversion.Modifiers, conversion.ParameterList.Span.End, depth);
                break;
            case LocalFunctionStatementSyntax local:
                InLocalFunction(local, depth);
                break;
            case PropertyDeclarationSyntax property:
                InProperty(property, property.Identifier.ValueText, property.AttributeLists, property.Modifiers, property.AccessorList, property.ExpressionBody, property.Identifier.Span.End, depth);
                break;
            case IndexerDeclarationSyntax indexer:
                InProperty(indexer, "this[]", indexer.AttributeLists, indexer.Modifiers, indexer.AccessorList, indexer.ExpressionBody, indexer.ParameterList.Span.End, depth);
                break;
            case EventDeclarationSyntax @event:
                InProperty(@event, @event.Identifier.ValueText, @event.AttributeLists, @event.Modifiers, @event.AccessorList, null, @event.Identifier.Span.End, depth);
                break;
            case VariableDeclaratorSyntax { Initializer.Value: LambdaExpressionSyntax or AnonymousMethodExpressionSyntax } declarator:
                InNamedLambda(declarator, depth);
                break;
            case InvocationExpressionSyntax invocation:
                AddInvocation(invocation);
                WalkChildren(node, depth);
                break;
            case ObjectCreationExpressionSyntax creation:
                AddCreation(creation);
                WalkChildren(node, depth);
                break;
            case ArgumentSyntax argument:
                AddMethodGroup(argument.Expression);
                WalkChildren(node, depth);
                break;
            case EqualsValueClauseSyntax initializer:
                AddMethodGroup(initializer.Value);
                WalkChildren(node, depth);
                break;
            case AssignmentExpressionSyntax assignment when assignment.Kind() is SyntaxKind.SimpleAssignmentExpression or SyntaxKind.AddAssignmentExpression or SyntaxKind.SubtractAssignmentExpression:
                AddMethodGroup(assignment.Right);
                WalkChildren(node, depth);
                break;
            default:
                WalkChildren(node, depth);
                break;
        }
    }

    private void WalkChildren(SyntaxNode node, int depth)
    {
        foreach (var child in node.ChildNodes())
        {
            Walk(child, depth + 1);
        }
    }

    private void InNamespace(BaseNamespaceDeclarationSyntax @namespace, int depth)
    {
        var saved = _container;
        _container = Join(_container, string.Concat(@namespace.Name.DescendantTokens().Select(t => t.ValueText)));
        WalkChildren(@namespace, depth);
        _container = saved;
    }

    private void InType(BaseTypeDeclarationSyntax type, SymbolKind kind, int arity, SyntaxList<AttributeListSyntax> attributes, SyntaxTokenList modifiers, SyntaxToken identifier, IEnumerable<ParameterSyntax>? primaryParameters, int depth)
    {
        var name = identifier.ValueText;
        var exported = _containerVisible && IsExported(modifiers, _inInterface);
        var draft = Declare(type, name, Join(_container, name), kind, arity, string.Empty, exported, HeadStart(type, attributes), TypeHeadEnd(type, identifier), callable: false);
        AddInherits(draft, type.BaseList);

        var saved = (_container, _owner, _containerVisible, _inInterface, _localFunctions);
        _container = draft.Qualified;
        _owner = draft;
        _containerVisible = exported;
        _inInterface = kind == SymbolKind.Interface;
        _localFunctions = [];
        _valueMembers.Push(ValueMembers(type, primaryParameters));
        _methodNames.Push(type is TypeDeclarationSyntax declaration ? MethodNames(declaration) : []);
        WalkChildren(type, depth);
        _methodNames.Pop();
        _valueMembers.Pop();
        (_container, _owner, _containerVisible, _inInterface, _localFunctions) = saved;
    }

    private void InMember(SyntaxNode node, string name, SymbolKind kind, BaseParameterListSyntax parameters, SyntaxList<AttributeListSyntax> attributes, SyntaxTokenList modifiers, int headEnd, int depth)
    {
        var exported = _containerVisible && IsExported(modifiers, _inInterface);
        var draft = Declare(node, name, Join(_container, name), kind, ParameterCount(parameters), ParameterTypes(parameters), exported, HeadStart(node, attributes), headEnd, callable: true);
        Enter(draft, node, depth, LocalFunctionNames(node));
    }

    private void InLocalFunction(LocalFunctionStatementSyntax local, int depth)
    {
        var name = local.Identifier.ValueText;
        var draft = Declare(local, name, Join(_container, name), SymbolKind.LocalFunction, ParameterCount(local.ParameterList), ParameterTypes(local.ParameterList), false, HeadStart(local, local.AttributeLists), EndOfHead(local.ParameterList, local.ConstraintClauses), callable: true);
        Enter(draft, local, depth, _localFunctions);
    }

    private void InNamedLambda(VariableDeclaratorSyntax declarator, int depth)
    {
        var name = declarator.Identifier.ValueText;
        var lambda = declarator.Initializer!.Value;
        var declaration = declarator.Parent as VariableDeclarationSyntax;
        var (arity, types, headEnd) = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => (1, string.Empty, simple.Block is null ? declarator.Span.End : simple.ArrowToken.Span.End),
            ParenthesizedLambdaExpressionSyntax parenthesized => (ParameterCount(parenthesized.ParameterList), ParameterTypes(parenthesized.ParameterList), parenthesized.Block is null ? declarator.Span.End : parenthesized.ArrowToken.Span.End),
            AnonymousMethodExpressionSyntax method => (method.ParameterList is null ? 0 : ParameterCount(method.ParameterList), method.ParameterList is null ? string.Empty : ParameterTypes(method.ParameterList), method.Block is null ? declarator.Span.End : method.ParameterList?.Span.End ?? method.DelegateKeyword.Span.End),
            _ => (0, string.Empty, declarator.Span.End),
        };
        var head = SyntaxText.Normalize(_text.ToString(TextSpan.FromBounds(declarator.SpanStart, headEnd)));
        var start = declaration?.SpanStart ?? declarator.SpanStart;
        var draft = Declare(declarator, name, Join(_container, name), SymbolKind.Lambda, arity, types, false, start, declarator.Span.End, callable: true);
        draft.Signature = declaration is null ? head : SyntaxText.Normalize(declaration.Type.ToString()) + " " + head;
        Enter(draft, declarator, depth, _localFunctions);
    }

    private void InProperty(SyntaxNode node, string name, SyntaxList<AttributeListSyntax> attributes, SyntaxTokenList modifiers, AccessorListSyntax? accessors, ArrowExpressionClauseSyntax? expressionBody, int headEnd, int depth)
    {
        var propertyQualified = Join(_container, name);
        var exported = _containerVisible && IsExported(modifiers, _inInterface);
        var headStart = HeadStart(node, attributes);
        Draft? expressionDraft = expressionBody is null
            ? null
            : Declare(node, "get", Join(propertyQualified, "get"), SymbolKind.Accessor, 0, string.Empty, exported, headStart, headEnd, callable: true);

        foreach (var child in node.ChildNodes())
        {
            if (child is AccessorListSyntax list)
            {
                foreach (var accessor in list.Accessors.Where(a => a.Body is not null || a.ExpressionBody is not null))
                {
                    var keyword = accessor.Keyword.ValueText;
                    var accessorExported = accessor.Modifiers.Count == 0 ? exported : exported && IsExported(accessor.Modifiers, false);
                    var draft = Declare(accessor, keyword, Join(propertyQualified, keyword), SymbolKind.Accessor, keyword == "get" ? 0 : 1, string.Empty, accessorExported, HeadStart(accessor, accessor.AttributeLists), accessor.Keyword.Span.End, callable: true);
                    Enter(draft, accessor, depth + 1, LocalFunctionNames(accessor));
                }
            }
            else if (expressionDraft is not null && ReferenceEquals(child, expressionBody))
            {
                Enter(expressionDraft, child, depth + 1, LocalFunctionNames(child));
            }
            else
            {
                Walk(child, depth + 1);
            }
        }
    }

    private void Enter(Draft draft, SyntaxNode node, int depth, HashSet<string> localFunctions)
    {
        var saved = (_container, _owner, _inInterface, _localFunctions);
        _container = draft.Qualified;
        _owner = draft;
        _inInterface = false;
        _localFunctions = localFunctions;
        WalkChildren(node, depth);
        (_container, _owner, _inInterface, _localFunctions) = saved;
    }

    private Draft Declare(SyntaxNode node, string name, string qualified, SymbolKind kind, int arity, string parameterTypes, bool exported, int headStart, int headEnd, bool callable)
    {
        if (_drafts.Count >= _limits.MaxSymbols)
        {
            throw new ParseLimitExceededException(SkipReasons.TooManySymbols);
        }

        var measures = SyntaxMeasures.Measure(node, callable);
        var draft = new Draft
        {
            Name = name,
            Qualified = qualified,
            Kind = kind,
            Arity = arity,
            ParameterTypesText = parameterTypes,
            StartLine = _text.Lines.GetLineFromPosition(headStart).LineNumber + 1,
            EndLine = _text.Lines.GetLineFromPosition(Math.Max(node.Span.End - 1, node.SpanStart)).LineNumber + 1,
            Exported = exported,
            Signature = SyntaxText.Normalize(_text.ToString(TextSpan.FromBounds(headStart, Math.Max(headEnd, headStart)))),
            ShapeHash = measures.ShapeHash,
            Complexity = measures.Complexity,
            Tokens = measures.Tokens,
            Body = callable ? _text.ToString(node.Span) : null,
        };
        _drafts.Add(draft);
        return draft;
    }

    private void AddInherits(Draft draft, BaseListSyntax? baseList)
    {
        if (baseList is null)
        {
            return;
        }

        foreach (var baseType in baseList.Types)
        {
            if (SimpleNameOf(baseType.Type) is { } name)
            {
                var arity = name is GenericNameSyntax generic ? generic.TypeArgumentList.Arguments.Count : 0;
                AddSite(draft, EdgeKind.Inherits, name.Identifier.ValueText, arity, "type:" + name.Identifier.ValueText, name.Identifier);
            }
        }
    }

    private void AddInvocation(InvocationExpressionSyntax invocation)
    {
        if (_owner is null)
        {
            return;
        }

        var arity = invocation.ArgumentList.Arguments.Count;
        switch (invocation.Expression)
        {
            case SimpleNameSyntax { Identifier.ValueText: "nameof" }:
                break;
            case SimpleNameSyntax name:
                AddSite(_owner, EdgeKind.Calls, name.Identifier.ValueText, arity, "this", name.Identifier);
                break;
            case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                AddSite(_owner, EdgeKind.Calls, access.Name.Identifier.ValueText, arity, ReceiverOf(access.Expression), access.Name.Identifier);
                break;
            case MemberBindingExpressionSyntax binding:
                AddSite(_owner, EdgeKind.Calls, binding.Name.Identifier.ValueText, arity, "other", binding.Name.Identifier);
                break;
        }
    }

    private void AddCreation(ObjectCreationExpressionSyntax creation)
    {
        if (_owner is null)
        {
            return;
        }

        var arity = creation.ArgumentList?.Arguments.Count ?? 0;
        if (SimpleNameOf(creation.Type) is { } name)
        {
            AddSite(_owner, EdgeKind.Calls, name.Identifier.ValueText, arity, "type:" + name.Identifier.ValueText, name.Identifier);
        }
        else if (creation.Type is PredefinedTypeSyntax predefined)
        {
            AddSite(_owner, EdgeKind.Calls, predefined.Keyword.Text, arity, "type:" + predefined.Keyword.Text, predefined.Keyword);
        }
    }

    // A bare name that is a local function of the enclosing member or a method of the enclosing type, used as a value:
    // a method group conversion, so any arity matches (-1).
    private void AddMethodGroup(ExpressionSyntax? expression)
    {
        if (_owner is not null
            && expression is IdentifierNameSyntax name
            && (_localFunctions.Contains(name.Identifier.ValueText) || (_methodNames.Count > 0 && _methodNames.Peek().Contains(name.Identifier.ValueText))))
        {
            AddSite(_owner, EdgeKind.Calls, name.Identifier.ValueText, -1, "this", name.Identifier);
        }
    }

    private void AddSite(Draft owner, EdgeKind kind, string name, int arity, string receiver, SyntaxToken at)
    {
        if (++_callSites > _limits.MaxCallSites)
        {
            throw new ParseLimitExceededException(SkipReasons.TooManyCallSites);
        }

        owner.CallSites.Add(new CallSite(kind, name, arity, receiver, _text.Lines.GetLineFromPosition(at.SpanStart).LineNumber + 1));
    }

    // 'this', 'base', a predefined type or a bare upper-case name that is not a member of an enclosing type is a type;
    // anything else is some other value.
    private string ReceiverOf(ExpressionSyntax expression) => expression switch
    {
        ThisExpressionSyntax => "this",
        BaseExpressionSyntax => "base",
        PredefinedTypeSyntax predefined => "type:" + predefined.Keyword.Text,
        SimpleNameSyntax name when char.IsUpper(name.Identifier.ValueText, 0) && !_valueMembers.Any(members => members.Contains(name.Identifier.ValueText)) => "type:" + name.Identifier.ValueText,
        _ => "other",
    };

    private static SimpleNameSyntax? SimpleNameOf(TypeSyntax type) => type switch
    {
        SimpleNameSyntax name => name,
        QualifiedNameSyntax qualified => qualified.Right,
        AliasQualifiedNameSyntax alias => alias.Name,
        _ => null,
    };

    private static SymbolKind TypeKind(TypeDeclarationSyntax type) => type.Kind() switch
    {
        SyntaxKind.ClassDeclaration => SymbolKind.Class,
        SyntaxKind.StructDeclaration => SymbolKind.Struct,
        SyntaxKind.InterfaceDeclaration => SymbolKind.Interface,
        _ => SymbolKind.Record,
    };

    private static int ParameterCount(BaseParameterListSyntax parameters) =>
        parameters.Parameters.Count - (parameters.Parameters.Count > 0 && parameters.Parameters[0].Modifiers.Any(SyntaxKind.ThisKeyword) ? 1 : 0);

    private static string ParameterTypes(BaseParameterListSyntax parameters) =>
        string.Join(',', parameters.Parameters.Select(p => SyntaxText.Normalize($"{string.Join(' ', p.Modifiers.Where(m => !m.IsKind(SyntaxKind.ThisKeyword) && !m.IsKind(SyntaxKind.ParamsKeyword)).Select(m => m.Text))} {p.Type}")).ToArray());

    private static int EndOfHead(ParameterListSyntax parameters, SyntaxList<TypeParameterConstraintClauseSyntax> constraints) =>
        constraints.Count > 0 ? constraints[^1].Span.End : parameters.Span.End;

    private static int HeadStart(SyntaxNode node, SyntaxList<AttributeListSyntax> attributes) =>
        attributes.Count == 0 ? node.SpanStart : attributes[^1].GetLastToken().GetNextToken().SpanStart;

    private static int TypeHeadEnd(BaseTypeDeclarationSyntax type, SyntaxToken identifier)
    {
        var closer = type.OpenBraceToken.IsMissing ? type.SemicolonToken : type.OpenBraceToken;
        return closer.IsMissing ? identifier.Span.End : closer.GetPreviousToken().Span.End;
    }

    private static HashSet<string> LocalFunctionNames(SyntaxNode node) =>
        [.. node.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Select(f => f.Identifier.ValueText)];

    private static HashSet<string> MethodNames(TypeDeclarationSyntax type) =>
        [.. type.Members.OfType<MethodDeclarationSyntax>().Select(m => m.Identifier.ValueText)];

    private static HashSet<string> ValueMembers(BaseTypeDeclarationSyntax type, IEnumerable<ParameterSyntax>? primaryParameters)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in primaryParameters ?? [])
        {
            names.Add(parameter.Identifier.ValueText);
        }

        if (type is EnumDeclarationSyntax @enum)
        {
            names.UnionWith(@enum.Members.Select(m => m.Identifier.ValueText));
        }
        else if (type is TypeDeclarationSyntax declaration)
        {
            foreach (var member in declaration.Members)
            {
                switch (member)
                {
                    case PropertyDeclarationSyntax property:
                        names.Add(property.Identifier.ValueText);
                        break;
                    case FieldDeclarationSyntax field:
                        names.UnionWith(field.Declaration.Variables.Select(v => v.Identifier.ValueText));
                        break;
                    case EventFieldDeclarationSyntax eventField:
                        names.UnionWith(eventField.Declaration.Variables.Select(v => v.Identifier.ValueText));
                        break;
                }
            }
        }

        return names;
    }

    // 'public' and 'protected' (alone or with 'internal') export; 'private' (also 'private protected') and 'internal' do not; an interface member with no
    // modifier is public. The caller adds that every enclosing type is visible.
    private static bool IsExported(SyntaxTokenList modifiers, bool inInterface)
    {
        var @private = modifiers.Any(SyntaxKind.PrivateKeyword);
        if (@private)
        {
            return false;
        }

        return modifiers.Any(SyntaxKind.PublicKeyword)
            || modifiers.Any(SyntaxKind.ProtectedKeyword)
            || (inInterface && !modifiers.Any(SyntaxKind.InternalKeyword));
    }

    private static string Join(string container, string name) => container.Length == 0 ? name : container + "." + name;

    private sealed class Draft
    {
        public required string Name { get; init; }

        public required string Qualified { get; init; }

        public required SymbolKind Kind { get; init; }

        public required int Arity { get; init; }

        public required string ParameterTypesText { get; init; }

        public required int StartLine { get; init; }

        public required int EndLine { get; init; }

        public required bool Exported { get; init; }

        public required string Signature { get; set; }

        public required string ShapeHash { get; init; }

        public required int Complexity { get; init; }

        public required IReadOnlyList<string> Tokens { get; init; }

        public required string? Body { get; init; }

        public List<CallSite> CallSites { get; } = [];

        public ParsedSymbol ToSymbol() =>
            new(Name, Qualified, Kind, Arity, ParameterTypesText, StartLine, EndLine, Exported, Signature, ShapeHash, Complexity, Tokens, Body, CallSites);
    }
}
