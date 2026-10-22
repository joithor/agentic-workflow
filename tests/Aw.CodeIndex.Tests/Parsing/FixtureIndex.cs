using System.Globalization;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Infrastructure.Parsing;

namespace Aw.CodeIndex.Tests.Parsing;

/// <summary>Parses a set of C# sources into symbols with refs and the call-site rows the resolver reads, the way a build would.</summary>
internal sealed class FixtureIndex
{
    private readonly Dictionary<SymbolRef, string> _keys = [];

    private FixtureIndex(IReadOnlyList<ResolutionSymbol> symbols, IReadOnlyList<CallSiteRow> rows, IReadOnlyList<(string File, ParsedSymbol Symbol, SymbolRef Ref)> parsed)
    {
        Symbols = symbols;
        CallSites = rows;
        Parsed = parsed;
        foreach (var (file, symbol, reference) in parsed)
        {
            _keys[reference] = string.Create(CultureInfo.InvariantCulture, $"{file}:{symbol.StartLine}");
        }
    }

    public IReadOnlyList<ResolutionSymbol> Symbols { get; }

    public IReadOnlyList<CallSiteRow> CallSites { get; }

    public IReadOnlyList<(string File, ParsedSymbol Symbol, SymbolRef Ref)> Parsed { get; }

    public static FixtureIndex From(IEnumerable<KeyValuePair<string, string>> sources)
    {
        var parser = new CSharpParser();
        var symbols = new List<ResolutionSymbol>();
        var rows = new List<CallSiteRow>();
        var parsed = new List<(string, ParsedSymbol, SymbolRef)>();
        foreach (var (path, text) in sources.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            var outcome = parser.Parse(path, text, CancellationToken.None).Value;
            foreach (var symbol in outcome.Symbols)
            {
                var reference = SymbolRef.From(string.Create(CultureInfo.InvariantCulture, $"fixture:{path}#{Clean(symbol.Qualified)}@{symbol.StartLine}"));
                parsed.Add((path, symbol, reference));
                symbols.Add(new ResolutionSymbol(reference, path, symbol.Name, symbol.Qualified, symbol.Kind, symbol.Arity, symbol.StartLine, symbol.EndLine));
                rows.AddRange(symbol.CallSites.Select(site => new CallSiteRow(reference, site)));
            }
        }

        return new FixtureIndex(symbols, rows, parsed);
    }

    /// <summary>The 'file:startLine' key of a symbol.</summary>
    public string KeyOf(SymbolRef reference) => _keys[reference];

    private static string Clean(string qualified) => new([.. qualified.Where(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != UnicodeCategory.Format)]);
}
