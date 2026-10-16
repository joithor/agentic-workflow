using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// The recorded Onyx documents ('onyx/documents.json': each a SearchDoc and its chunk texts) and a deterministic search
/// over them: a document scores by the share of query terms it contains (plus a tiny weight per occurrence), ties broken
/// by newest 'updated_at', then document id, at most 50 (Onyx's keyword retrieval, one document per id). Not Onyx's
/// ranking (Sindri ranks, D3); only stable and explainable.
/// </summary>
internal static class OnyxIndex
{
    private static readonly HashSet<string> _stopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "about", "does", "for", "how", "in", "is", "it", "of", "on", "or", "the", "to", "was", "what", "which",
    };

    private static readonly Lazy<List<OnyxDocument>> _documents = new(Load);

    public static IReadOnlyList<OnyxDocument> Documents => _documents.Value;

    public static JsonArray Search(OnyxSearchRequest request, IReadOnlyList<OnyxDocument> documents)
    {
        var candidates = documents.Where(d => Matches(d, request)).ToList();
        if (request.Query.Trim().Length == 0)
        {
            // Onyx answers an empty query with random_retrieval; the fake keeps it deterministic (document id order).
            return [.. candidates.OrderBy(d => d.Id, StringComparer.Ordinal).Take(OnyxSearchRequest.MaxHits).Select(d => Hit(d, score: null, 0, []))];
        }

        var terms = Terms(request.Query).Distinct(StringComparer.Ordinal).ToList();
        var hits = candidates
            .Select(d => (Document: d, Score: Score(d, terms, out var chunk), Chunk: chunk))
            .Where(h => h.Score > 0)
            .OrderByDescending(h => h.Score)
            .ThenByDescending(h => h.Document.UpdatedAt)
            .ThenBy(h => h.Document.Id, StringComparer.Ordinal)
            .Take(OnyxSearchRequest.MaxHits)
            .Select(h => Hit(h.Document, h.Score, h.Chunk, terms));
        return [.. hits];
    }

    /// <summary>A synthetic Linear issue as the connector records it: the search doc (identifier '[KEY] title', link, created_at in metadata) and its chunks.</summary>
    /// <param name="issue">The issue.</param>
    /// <returns>The document.</returns>
    public static OnyxDocument From(SyntheticIssue issue)
    {
        var doc = new JsonObject
        {
            ["document_id"] = issue.Id,
            ["semantic_identifier"] = $"[{issue.Key}] {issue.Title}",
            ["link"] = "https://linear.app/fixture-labs/issue/" + issue.Key,
            ["blurb"] = issue.Chunks[0],
            ["source_type"] = "linear",
            ["boost"] = 0,
            ["hidden"] = false,
            ["metadata"] = new JsonObject { ["team"] = "Split Panes", ["state"] = "Done", ["created_at"] = issue.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) },
            ["updated_at"] = issue.UpdatedAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ["primary_owners"] = new JsonArray("Avery Fixture"),
            ["secondary_owners"] = new JsonArray("Avery Fixture"),
        };
        return new OnyxDocument(issue.Id, $"[{issue.Key}] {issue.Title}", "linear", issue.UpdatedAt, doc, issue.Chunks);
    }

    public static IEnumerable<string> Terms(string text) =>
        new string([.. text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !_stopWords.Contains(t));

    private static double Score(OnyxDocument document, List<string> terms, out int bestChunk)
    {
        var counts = document.Chunks.Select(c => Terms(c).Count(terms.Contains)).ToList();
        bestChunk = counts.IndexOf(counts.Max());
        var words = Terms(document.Title).Concat(document.Chunks.SelectMany(Terms)).ToHashSet(StringComparer.Ordinal);
        var matched = terms.Count(words.Contains);
        return matched == 0 ? 0 : Math.Round(((double)matched / terms.Count) + (counts.Sum() * 0.001), 4, MidpointRounding.AwayFromZero);
    }

    private static bool Matches(OnyxDocument document, OnyxSearchRequest request) =>
        (request.SourceTypes is null || request.SourceTypes.Contains(document.SourceType, StringComparer.Ordinal))
        && (request.UpdatedFrom is null || document.UpdatedAt >= request.UpdatedFrom)
        && (request.UpdatedTo is null || document.UpdatedAt <= request.UpdatedTo);

    /// <summary>A SearchDoc as v4.9.0 serializes it (onyx/context/search/models.py SearchDoc): the recording plus the per-hit fields.</summary>
    private static JsonObject Hit(OnyxDocument document, double? score, int chunk, List<string> terms)
    {
        var hit = (JsonObject)document.SearchDoc.DeepClone();
        hit["chunk_ind"] = chunk;
        hit["score"] = score;
        hit["is_relevant"] = null;
        hit["relevance_explanation"] = null;
        hit["match_highlights"] = new JsonArray([.. Highlights(document.Chunks[chunk], terms)]);
        hit["source_types"] = null;
        hit["is_internet"] = false;
        hit["file_id"] = null;
        return hit;
    }

    private static IEnumerable<JsonNode?> Highlights(string chunk, List<string> terms) =>
        chunk.Split(". ")
            .Where(sentence => Terms(sentence).Any(terms.Contains))
            .Take(2)
            .Select(sentence => JsonValue.Create(sentence));

    private static List<OnyxDocument> Load()
    {
        var file = JsonNode.Parse(SplitPanesFixture.ReadText("onyx/documents.json"));
        return [.. (file?["documents"]?.AsArray() ?? []).OfType<JsonObject>().Select(Document)];
    }

    private static OnyxDocument Document(JsonObject entry)
    {
        var doc = entry["search_doc"]?.AsObject() ?? throw new InvalidOperationException("onyx/documents.json: an entry has no search_doc.");
        return new OnyxDocument(
            Id: (string?)doc["document_id"] ?? throw new InvalidOperationException("onyx/documents.json: a search_doc has no document_id."),
            Title: (string?)doc["semantic_identifier"] ?? "",
            SourceType: (string?)doc["source_type"] ?? "",
            UpdatedAt: DateTimeOffset.Parse((string?)doc["updated_at"] ?? "", CultureInfo.InvariantCulture),
            SearchDoc: doc,
            Chunks: [.. (entry["chunks"]?.AsArray() ?? []).Select(c => (string?)c ?? "")]);
    }
}
