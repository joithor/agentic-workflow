using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Resolution;

/// <summary>
/// Resolves raw syntax call sites to edges by name (spec §5.4), with no I/O: the repo's symbols and its manifest
/// directories in, ranked edges out.
/// </summary>
public sealed class CallResolver
{
    private readonly Dictionary<SymbolRef, Candidate> _bySymbol = [];
    private readonly Dictionary<string, List<Candidate>> _byName = new(StringComparer.Ordinal);

    /// <summary>Creates a resolver over a repo's symbols.</summary>
    /// <param name="symbols">Every symbol of the repo.</param>
    /// <param name="projectDirectories">The directories that hold a manifest, repo-relative with '/' and no trailing slash; the root is the empty string. The nearest one is a file's project directory.</param>
    public CallResolver(IReadOnlyList<ResolutionSymbol> symbols, IReadOnlyCollection<string> projectDirectories)
    {
        var typesByFile = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var symbol in symbols.Where(s => IsType(s.Kind)))
        {
            if (!typesByFile.TryGetValue(symbol.File, out var types))
            {
                typesByFile[symbol.File] = types = new HashSet<string>(StringComparer.Ordinal);
            }

            types.Add(symbol.Qualified);
        }

        var directories = new HashSet<string>(projectDirectories, StringComparer.Ordinal);
        foreach (var symbol in symbols.Where(s => IsCallable(s.Kind) || IsType(s.Kind)))
        {
            var candidate = new Candidate(
                symbol,
                NamespaceOf(symbol, typesByFile.GetValueOrDefault(symbol.File)),
                ProjectDirectoryOf(symbol.File, directories));
            _bySymbol[symbol.Ref] = candidate;
            if (!_byName.TryGetValue(symbol.Name, out var named))
            {
                _byName[symbol.Name] = named = [];
            }

            named.Add(candidate);
        }
    }

    /// <summary>Resolves call sites to edges.</summary>
    /// <param name="rows">The call sites with their callers, in source order.</param>
    /// <returns>The edges: one per call, or one per candidate (at most eight) for an ambiguous one, without duplicates.</returns>
    public IReadOnlyList<EdgeRecord> Resolve(IEnumerable<CallSiteRow> rows)
    {
        var edges = new List<EdgeRecord>();
        var seen = new HashSet<(SymbolRef, string, EdgeKind, int, SymbolRef?)>();
        foreach (var row in rows)
        {
            if (!_bySymbol.TryGetValue(row.Caller, out var caller))
            {
                continue;
            }

            foreach (var edge in ResolveOne(caller, row))
            {
                if (seen.Add((edge.Source, edge.TargetName, edge.Kind, edge.Line, edge.Target)))
                {
                    edges.Add(edge);
                }
            }
        }

        return edges;
    }

    /// <summary>Renders a candidate's place among the name's matches.</summary>
    /// <param name="rank">The 1-based rank.</param>
    /// <param name="candidates">The true number of matches.</param>
    /// <returns>For example '1 of 300'.</returns>
    public static string RankText(int rank, int candidates) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{rank} of {candidates}");

    private static bool IsType(SymbolKind kind) => kind is SymbolKind.Class or SymbolKind.Record or SymbolKind.Struct or SymbolKind.Interface or SymbolKind.Enum;

    private static bool IsCallable(SymbolKind kind) =>
        kind is SymbolKind.Function or SymbolKind.Method or SymbolKind.Constructor or SymbolKind.Accessor or SymbolKind.LocalFunction or SymbolKind.Lambda;

    // The namespace is the qualified name up to the outermost type declared in the file (a nested type stays in its outer type's namespace).
    private static string NamespaceOf(ResolutionSymbol symbol, HashSet<string>? typesInFile)
    {
        var qualified = symbol.Qualified;
        var end = qualified.IndexOf('.', StringComparison.Ordinal);
        while (end >= 0)
        {
            if (typesInFile is not null && typesInFile.Contains(qualified[..end]))
            {
                return Parent(qualified[..end]);
            }

            end = qualified.IndexOf('.', end + 1);
        }

        return Parent(qualified);
    }

    private static string Parent(string qualified)
    {
        var dot = qualified.LastIndexOf('.');
        return dot < 0 ? string.Empty : qualified[..dot];
    }

    private static string ProjectDirectoryOf(string file, HashSet<string> directories)
    {
        var slash = file.LastIndexOf('/');
        while (slash >= 0)
        {
            var directory = file[..slash];
            if (directories.Contains(directory))
            {
                return directory;
            }

            slash = directory.LastIndexOf('/');
        }

        return string.Empty;
    }

    private EdgeRecord[] ResolveOne(Candidate caller, CallSiteRow row)
    {
        var site = row.Site;
        var matches = new List<Candidate>();
        if (_byName.TryGetValue(site.Name, out var named))
        {
            var wantTypes = site.Kind == EdgeKind.Inherits;
            foreach (var candidate in named)
            {
                if ((wantTypes ? IsType(candidate.Symbol.Kind) : IsCallable(candidate.Symbol.Kind))
                    && (site.Arity < 0 || candidate.Symbol.Arity == site.Arity))
                {
                    matches.Add(candidate);
                }
            }
        }

        if (matches.Count == 0)
        {
            return [new EdgeRecord(row.Caller, Target: null, site.Name, site.Kind, EdgeBasis.Syntax, Confidence.Unresolved, site.Line, 0)];
        }

        if (matches.Count == 1)
        {
            var only = matches[0];
            return [new EdgeRecord(row.Caller, only.Symbol.Ref, site.Name, site.Kind, EdgeBasis.Syntax, SingleConfidence(site, only.Symbol), site.Line, 1)];
        }

        matches.Sort((a, b) => Compare(caller, a, b));
        var kept = Math.Min(matches.Count, CodeIndexRules.MaxAmbiguousEdges);
        var edges = new EdgeRecord[kept];
        for (var i = 0; i < kept; i++)
        {
            edges[i] = new EdgeRecord(row.Caller, matches[i].Symbol.Ref, site.Name, site.Kind, EdgeBasis.Syntax, Confidence.Ambiguous, site.Line, matches.Count);
        }

        return edges;
    }

    private static Confidence SingleConfidence(CallSite site, ResolutionSymbol only)
    {
        if (site.Receiver == "this")
        {
            return Confidence.Certain;
        }

        const string typePrefix = "type:";
        if (site.Receiver.StartsWith(typePrefix, StringComparison.Ordinal))
        {
            var type = site.Receiver.AsSpan(typePrefix.Length);
            var container = site.Kind == EdgeKind.Inherits ? only.Qualified : Parent(only.Qualified);
            if (container.AsSpan().EndsWith(type, StringComparison.Ordinal)
                && (container.Length == type.Length || container[container.Length - type.Length - 1] == '.'))
            {
                return Confidence.Certain;
            }
        }

        return Confidence.Likely;
    }

    private static int Compare(Candidate caller, Candidate a, Candidate b)
    {
        var order = Closeness(caller, a).CompareTo(Closeness(caller, b));
        if (order != 0)
        {
            return order;
        }

        order = string.CompareOrdinal(a.Symbol.File, b.Symbol.File);
        return order != 0 ? order : a.Symbol.StartLine.CompareTo(b.Symbol.StartLine);
    }

    // Lower is closer: 0 same file, 1 same namespace, 2 same project directory, 3 anywhere else.
    private static int Closeness(Candidate caller, Candidate other) =>
        string.Equals(caller.Symbol.File, other.Symbol.File, StringComparison.Ordinal) ? 0
        : string.Equals(caller.Namespace, other.Namespace, StringComparison.Ordinal) ? 1
        : string.Equals(caller.ProjectDirectory, other.ProjectDirectory, StringComparison.Ordinal) ? 2
        : 3;

    private sealed record Candidate(ResolutionSymbol Symbol, string Namespace, string ProjectDirectory);
}
