using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>
/// Names a file's symbols (spec §3): 'repo:path#qualified[/arity][@h]', where '@h' (the first four hex digits of the hash of the
/// parameter-type text) separates same-name overloads in one file and equal hashes take '-2', '-3' by order.
/// </summary>
internal static class SymbolRefs
{
    private const int QualifiedLimit = 400;

    /// <summary>Assigns a ref to each symbol of one file.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="symbols">The file's parsed symbols, in source order.</param>
    /// <returns>One ref per symbol, in the same order; null for a symbol whose name cannot make a valid ref (it is not stored).</returns>
    public static IReadOnlyList<SymbolRef?> Assign(RepoName repo, string path, IReadOnlyList<ParsedSymbol> symbols)
    {
        var bases = symbols.Select(s => Base(repo, path, s)).ToList();
        var overloaded = bases.Where(b => b is not null).GroupBy(b => b, StringComparer.Ordinal).Where(g => g.Skip(1).Any()).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var taken = new Dictionary<string, int>(StringComparer.Ordinal);
        var refs = new List<SymbolRef?>(symbols.Count);
        for (var i = 0; i < symbols.Count; i++)
        {
            refs.Add(bases[i] is { } text ? Unique(overloaded.Contains(text) ? text + "@" + Hash(symbols[i].ParameterTypesText) : text, taken) : null);
        }

        return refs;
    }

    private static string? Base(RepoName repo, string path, ParsedSymbol symbol)
    {
        var qualified = Clean(symbol.Qualified);
        if (qualified.Length == 0)
        {
            return null;
        }

        var callable = symbol.Kind is not (SymbolKind.Class or SymbolKind.Record or SymbolKind.Struct or SymbolKind.Interface or SymbolKind.Enum or SymbolKind.GraphSymbol);
        var arity = callable ? "/" + symbol.Arity.ToString(CultureInfo.InvariantCulture) : string.Empty;
        return string.Concat(repo.Value, ":", path, "#", qualified.Length > QualifiedLimit ? qualified[..QualifiedLimit] : qualified, arity);
    }

    private static SymbolRef? Unique(string text, Dictionary<string, int> taken)
    {
        var count = taken.GetValueOrDefault(text) + 1;
        taken[text] = count;
        return SymbolRef.TryFrom(count == 1 ? text : text + "-" + count.ToString(CultureInfo.InvariantCulture), out var reference) ? reference : null;
    }

    private static string Hash(string parameterTypes) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(parameterTypes)).AsSpan(0, 2));

    private static string Clean(string qualified)
        => new([.. qualified.Where(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != UnicodeCategory.Format)]);
}
