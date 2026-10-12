using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Tests.Cqrs;

/// <summary>
/// ARCH-CQRS-003 (API spec §2.5, §3.3; UI spec E-12): every ICommand and IIssue type has a catalog row; a plain one has
/// exactly one row without a variant, and a classified one (IClassifiedCommand) has variant rows only.
/// </summary>
internal static class CatalogCoverageRules
{
    public const string Rule = "ARCH-CQRS-003";

    public static IReadOnlyList<string> Check(IEnumerable<Type> types, ICommandCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. types.Where(IsCommand).Select(type => Problem(type, catalog.For(type))).OfType<string>()];
    }

    private static string? Problem(Type type, IReadOnlyList<CommandRow> rows)
    {
        var classified = typeof(IClassifiedCommand).IsAssignableFrom(type);
        var problem = rows.Count == 0 ? "has no command catalog row; add one in Host/Catalog"
            : classified && rows.Any(row => row.Variant is null) ? "is classified, so every row needs a variant ('<Command>:<variant>')"
            : !classified && (rows.Count != 1 || rows[0].Variant is not null) ? "is a plain command, so it takes exactly one row without a variant"
            : null;
        return problem is null ? null : $"{Rule}: {type.Name} {problem}";
    }

    private static bool IsCommand(Type type) =>
        type is { IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICommand<>) || i.GetGenericTypeDefinition() == typeof(IIssue<>)));
}
