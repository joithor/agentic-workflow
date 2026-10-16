using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Onyx;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linking;

/// <summary>
/// The rules-first linker (spec §13): issue keys and URLs, repo paths, identifier tokens and quoted titles in a record's scrubbed text, matched to the map's nodes and to other records.
/// Pure and idempotent: the same text and graph give the same links. Plain English words never link.
/// </summary>
public static partial class RulesLinker
{
    private const double Exact = 1.0;
    private const double ByPath = 0.8;
    private const double ByName = 0.7;

    /// <summary>Links one record's text.</summary>
    /// <param name="record">The record the text belongs to (it never links to itself).</param>
    /// <param name="text">The sanitized, scrubbed text.</param>
    /// <param name="graph">The map to link into.</param>
    /// <param name="known">Every intent record the sync knows, for the issue-key and URL rules.</param>
    /// <param name="teams">The profile's tracker teams: only their keys link.</param>
    /// <returns>The links, ordered by target then rule.</returns>
    public static IReadOnlyList<IntentLink> Link(RecordKey record, string text, GraphSnapshot graph, IReadOnlyList<IntentRecord> known, IReadOnlySet<string> teams)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(teams);
        var run = new Run(record, text, graph, known, teams);
        run.Urls();
        run.Paths();
        run.QuotedTitles();
        run.IssueKeys();
        run.IdentifierTokens();
        return run.Result();
    }

    private sealed class Run(RecordKey record, string text, GraphSnapshot graph, IReadOnlyList<IntentRecord> known, IReadOnlySet<string> teams)
    {
        private readonly Dictionary<(string Target, LinkRule Rule), IntentLink> _found = [];
        private readonly StringBuilder _rest = new(text);

        public IReadOnlyList<IntentLink> Result() =>
            [.. _found.Values.OrderBy(l => l.Target.Value, StringComparer.Ordinal).ThenBy(l => l.Rule)];

        public void Urls()
        {
            foreach (var match in LinearUrl.Matches(_rest.ToString()).Where(m => RecordFor(m.Groups["key"].Value, known, teams) is not null).ToList())
            {
                LinkRecord(match, key: match.Groups["key"].Value, LinkRule.Url);
                Mask(match);
            }
        }

        public void Paths()
        {
            foreach (var match in RepoPath.Matches(_rest.ToString()).ToList())
            {
                if (graph.Nodes.FirstOrDefault(n => n.Key.Value == "code:" + match.Value) is { } code)
                {
                    Add(code.Key.Value, LinkRule.Path, ByPath, match.Value);
                    LinkImplemented(code, match.Value);
                    Mask(match);
                }
            }
        }

        public void QuotedTitles()
        {
            var byLabel = graph.Nodes.Where(n => n.Key.Kind is not (NodeKind.Record or NodeKind.Decision)).ToLookup(n => n.Label.Trim(), StringComparer.OrdinalIgnoreCase);
            foreach (var match in Quoted.Matches(_rest.ToString()).ToList())
            {
                var nodes = byLabel[match.Groups["title"].Value.Trim()].ToList();
                if (nodes.Count == 1)
                {
                    Add(nodes[0].Key.Value, LinkRule.QuotedTitle, ByName, match.Groups["title"].Value);
                    Mask(match);
                }
            }
        }

        public void IssueKeys()
        {
            foreach (var match in IssueKey.Matches(_rest.ToString()).ToList())
            {
                LinkRecord(match, match.Value, LinkRule.IssueKey);
            }
        }

        public void IdentifierTokens()
        {
            var identifiers = Identifiers(graph);
            foreach (var match in Token.Matches(_rest.ToString()).ToList())
            {
                if (identifiers.TryGetValue(match.Value, out var nodes) && nodes.Count == 1)
                {
                    Add(nodes.First(), LinkRule.Identifier, ByName, match.Value);
                }
            }
        }

        private void LinkRecord(Match m, string key, LinkRule rule)
        {
            if (RecordFor(key, known, teams) is { } target && target.Key.Value != record.Value)
            {
                Add("record:" + target.Key.Value, rule, Exact, m.Value);
            }
        }

        private void LinkImplemented(GraphNode code, string span)
        {
            foreach (var edge in graph.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Value == code.Key.Value && e.Provenance != Provenance.Model))
            {
                Add(edge.To.Value, LinkRule.Path, ByPath, span);
            }
        }

        private void Add(string target, LinkRule rule, double confidence, string span)
        {
            if (NodeKey.TryFrom(target, out var key) && !_found.ContainsKey((target, rule)))
            {
                _found[(target, rule)] = new IntentLink(record, key, rule, confidence, Hash(span));
            }
        }

        private void Mask(Match match) => _ = _rest.Remove(match.Index, match.Length).Insert(match.Index, new string(' ', match.Length));
    }

    private static IntentRecord? RecordFor(string issueKey, IReadOnlyList<IntentRecord> known, IReadOnlySet<string> teams) =>
        teams.Contains(issueKey[..issueKey.IndexOf('-', StringComparison.Ordinal)])
            ? known.FirstOrDefault(r => r.Aliases.Contains(issueKey, StringComparer.Ordinal))
            : null;

    private static Dictionary<string, SortedSet<string>> Identifiers(GraphSnapshot graph)
    {
        var map = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        void Offer(string candidate, string target)
        {
            if (IsIdentifier(candidate))
            {
                if (!map.TryGetValue(candidate, out var set))
                {
                    map[candidate] = set = new SortedSet<string>(StringComparer.Ordinal);
                }

                _ = set.Add(target);
            }
        }

        foreach (var node in graph.Nodes.Where(n => n.Key.Kind is not (NodeKind.Record or NodeKind.Decision)))
        {
            Offer(node.Key.Name, node.Key.Value);
            Offer(Tail(node.Key.Name), node.Key.Value);
            Offer(node.Label, node.Key.Value);
        }

        foreach (var alias in graph.Aliases.Where(a => a.Alias.Kind is not (NodeKind.Record or NodeKind.Decision)))
        {
            Offer(alias.Alias.Name, alias.Target.Value);
            Offer(Tail(alias.Alias.Name), alias.Target.Value);
        }

        return map;
    }

    private static string Tail(string name) => name[(name.LastIndexOf('/') + 1)..];

    /// <summary>Whether a candidate looks like a code identifier (a separator or a camelCase hump) and so can't be a plain English word.</summary>
    private static bool IsIdentifier(string candidate)
    {
        if (candidate.Length < 4 || !Token.IsMatch(candidate) || Token.Match(candidate).Value.Length != candidate.Length)
        {
            return false;
        }

        return candidate.AsSpan().IndexOfAny("-_.") is > 0
            || candidate.Zip(candidate.Skip(1)).Any(p => char.IsLower(p.First) && char.IsUpper(p.Second));
    }

    private static string Hash(string span) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(span)));

    [GeneratedRegex(@"https://linear\.app/[^/\s]+/issue/(?<key>" + OnyxRecordKeys.TeamKeyPattern + @"-[0-9]+)(?:/[^\s]*)?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinearUrl { get; }

    [GeneratedRegex(@"(?<![A-Za-z0-9-])" + OnyxRecordKeys.TeamKeyPattern + @"-[0-9]+(?![A-Za-z0-9-])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IssueKey { get; }

    [GeneratedRegex(@"(?<![\w/.\-])(?:[\w.\-]+/)+[\w\-]+(?:\.[A-Za-z0-9]+)+(?![\w/\-])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoPath { get; }

    [GeneratedRegex("(?<![\\w])[\"'`“‘](?<title>[^\"'`“”‘’\\n]{2,120})[\"'`”’](?![\\w])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Quoted { get; }

    [GeneratedRegex(@"(?<![A-Za-z0-9_\-.])[A-Za-z_][A-Za-z0-9_\-.]*[A-Za-z0-9_](?![A-Za-z0-9_\-]|\.[A-Za-z0-9_])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token { get; }
}
