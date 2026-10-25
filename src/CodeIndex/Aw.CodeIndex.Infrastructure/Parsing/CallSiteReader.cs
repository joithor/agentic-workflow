using Aw.CodeIndex.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>Reads the call sites of one syntax node by its shape alone (spec §5.3): no binding, no types.</summary>
internal static class CallSiteReader
{
    public static RawSite? Invocation(InvocationExpressionSyntax invocation, Func<string, bool> isValueMember)
    {
        var arity = invocation.ArgumentList.Arguments.Count;
        return invocation.Expression switch
        {
            SimpleNameSyntax { Identifier.ValueText: "nameof" } => null,
            SimpleNameSyntax name => Calls(name.Identifier, arity, "this"),
            MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) =>
                Calls(access.Name.Identifier, arity, ReceiverOf(access.Expression, isValueMember)),
            MemberBindingExpressionSyntax binding => Calls(binding.Name.Identifier, arity, "other"),
            _ => null,
        };
    }

    public static RawSite? Creation(ObjectCreationExpressionSyntax creation)
    {
        var arity = creation.ArgumentList?.Arguments.Count ?? 0;
        if (SimpleNameOf(creation.Type) is { } name)
        {
            return Calls(name.Identifier, arity, "type:" + name.Identifier.ValueText);
        }

        return creation.Type is PredefinedTypeSyntax predefined ? Calls(predefined.Keyword, arity, "type:" + predefined.Keyword.Text) : null;
    }

    public static IEnumerable<RawSite> Inherits(BaseListSyntax? baseList)
    {
        foreach (var baseType in baseList?.Types ?? [])
        {
            if (SimpleNameOf(baseType.Type) is { } name)
            {
                var arity = name is GenericNameSyntax generic ? generic.TypeArgumentList.Arguments.Count : 0;
                yield return new RawSite { Kind = EdgeKind.Inherits, Name = name.Identifier.ValueText, Arity = arity, Receiver = "type:" + name.Identifier.ValueText, At = name.Identifier };
            }
        }
    }

    public static RawSite MethodGroup(IdentifierNameSyntax name) => Calls(name.Identifier, arity: -1, "this");

    // 'this', 'base', a predefined type or a bare upper-case name that is not a member of an enclosing type is a type; anything else is some other value.
    private static string ReceiverOf(ExpressionSyntax expression, Func<string, bool> isValueMember) => expression switch
    {
        ThisExpressionSyntax => "this",
        BaseExpressionSyntax => "base",
        PredefinedTypeSyntax predefined => "type:" + predefined.Keyword.Text,
        SimpleNameSyntax name when char.IsUpper(name.Identifier.ValueText, 0) && !isValueMember(name.Identifier.ValueText) => "type:" + name.Identifier.ValueText,
        _ => "other",
    };

    private static RawSite Calls(SyntaxToken at, int arity, string receiver) =>
        new() { Kind = EdgeKind.Calls, Name = at.ValueText.Length == 0 ? at.Text : at.ValueText, Arity = arity, Receiver = receiver, At = at };

    private static SimpleNameSyntax? SimpleNameOf(TypeSyntax type) => type switch
    {
        SimpleNameSyntax name => name,
        QualifiedNameSyntax qualified => qualified.Right,
        AliasQualifiedNameSyntax alias => alias.Name,
        _ => null,
    };
}
