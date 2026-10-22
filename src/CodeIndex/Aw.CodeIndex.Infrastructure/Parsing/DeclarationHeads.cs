using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>The syntax facts a declaration's symbol is built from: where its head starts and ends, its parameters, whether it is exported, and the names a scope holds.</summary>
internal static class DeclarationHeads
{
    public static string Join(string container, string name) => container.Length == 0 ? name : container + "." + name;

    public static int ParameterCount(BaseParameterListSyntax parameters)
    {
        var all = parameters.Parameters;
        return all.Count - (all.Count > 0 && all[0].Modifiers.Any(SyntaxKind.ThisKeyword) ? 1 : 0);
    }

    public static string ParameterTypes(BaseParameterListSyntax parameters) =>
        string.Join(',', parameters.Parameters.Select(ParameterType));

    public static int HeadStart(SyntaxNode node, SyntaxList<AttributeListSyntax> attributes) =>
        attributes.Count == 0 ? node.SpanStart : attributes[^1].GetLastToken().GetNextToken().SpanStart;

    public static int EndOfHead(ParameterListSyntax parameters, SyntaxList<TypeParameterConstraintClauseSyntax> constraints) =>
        constraints.Count > 0 ? constraints[^1].Span.End : parameters.Span.End;

    public static int TypeHeadEnd(BaseTypeDeclarationSyntax type)
    {
        // A record with no body has no brace at all (a default token, which is not 'missing'), so absent and missing both fall through.
        var closer = IsPresent(type.OpenBraceToken) ? type.OpenBraceToken : type.SemicolonToken;
        return IsPresent(closer) ? closer.GetPreviousToken().Span.End : type.Identifier.Span.End;
    }

    private static bool IsPresent(SyntaxToken token) => !token.IsKind(SyntaxKind.None) && !token.IsMissing;

    // 'public' and 'protected' (alone or with 'internal') export; 'private' (also 'private protected') and 'internal' do not; an interface
    // member with no modifier is public. The caller adds that every enclosing type is visible.
    public static bool IsExported(SyntaxTokenList modifiers, bool inInterface)
    {
        if (modifiers.Any(SyntaxKind.PrivateKeyword))
        {
            return false;
        }

        return modifiers.Any(SyntaxKind.PublicKeyword)
            || modifiers.Any(SyntaxKind.ProtectedKeyword)
            || (inInterface && !modifiers.Any(SyntaxKind.InternalKeyword));
    }

    public static HashSet<string> LocalFunctionNames(SyntaxNode node) =>
        [.. node.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Select(f => f.Identifier.ValueText)];

    public static HashSet<string> MethodNames(BaseTypeDeclarationSyntax type) =>
        type is TypeDeclarationSyntax declaration ? [.. declaration.Members.OfType<MethodDeclarationSyntax>().Select(m => m.Identifier.ValueText)] : [];

    // The names of a type's properties, fields, events, enum members and primary-constructor parameters: an upper-case receiver of one of these is a value, not a type.
    public static HashSet<string> ValueMembers(BaseTypeDeclarationSyntax type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (type is EnumDeclarationSyntax @enum)
        {
            names.UnionWith(@enum.Members.Select(m => m.Identifier.ValueText));
        }
        else if (type is TypeDeclarationSyntax declaration)
        {
            names.UnionWith(declaration.ParameterList?.Parameters.Select(p => p.Identifier.ValueText) ?? []);
            foreach (var member in declaration.Members)
            {
                names.UnionWith(MemberNames(member));
            }
        }

        return names;
    }

    private static IEnumerable<string> MemberNames(MemberDeclarationSyntax member) => member switch
    {
        PropertyDeclarationSyntax property => [property.Identifier.ValueText],
        FieldDeclarationSyntax field => field.Declaration.Variables.Select(v => v.Identifier.ValueText),
        EventFieldDeclarationSyntax eventField => eventField.Declaration.Variables.Select(v => v.Identifier.ValueText),
        _ => [],
    };

    private static string ParameterType(ParameterSyntax parameter)
    {
        var modifiers = parameter.Modifiers.Where(m => !m.IsKind(SyntaxKind.ThisKeyword) && !m.IsKind(SyntaxKind.ParamsKeyword)).Select(m => m.Text);
        return SyntaxText.Normalize(string.Join(' ', modifiers) + " " + parameter.Type?.ToString());
    }
}
