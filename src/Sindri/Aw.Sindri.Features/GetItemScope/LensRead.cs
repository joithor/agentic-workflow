using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.GetItemScope;

/// <summary>The four lens entries of a lenses.json: each lens's anchors and the map version it read ('v&lt;N&gt;@&lt;commit&gt;').</summary>
internal sealed class LensRead
{
    private static readonly string[] _names = ["code", "product", "related", "history"];

    private readonly List<(string Lens, string Version, string Commit, IReadOnlyList<string> Anchors)> _lenses;

    private LensRead(List<(string Lens, string Version, string Commit, IReadOnlyList<string> Anchors)> lenses)
    {
        _lenses = lenses;
    }

    /// <summary>Reads <paramref name="text"/>; null when it is no JSON object, a lens is missing, or a lens's mapVersion is not 'v&lt;N&gt;@&lt;commit&gt;'.</summary>
    internal static LensRead? Of(string text)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        var lenses = new List<(string, string, string, IReadOnlyList<string>)>();
        foreach (var lens in _names)
        {
            if (root is not JsonObject top || top[lens] is not JsonObject entry || Split(Text(entry["mapVersion"])) is not { } version)
            {
                return null;
            }

            var anchors = entry["anchors"] is JsonArray listed ? listed.Select(Text).OfType<string>().ToList() : [];
            lenses.Add((lens, version.Version, version.Commit, anchors));
        }

        return new LensRead(lenses);
    }

    /// <summary>The scope: anchors distinct by (key, version) in ordinal order; a common version and commit only when all four lenses agree.</summary>
    internal ItemScope Scope(string packetHash, DateTimeOffset frozenAt)
    {
        var anchors = _lenses.SelectMany(static l => l.Anchors.Select(a => new ScopeAnchor(a, l.Version)))
            .Distinct()
            .OrderBy(static a => a.Key, StringComparer.Ordinal).ThenBy(static a => a.MapVersion, StringComparer.Ordinal)
            .ToList();
        string Version(string lens) => _lenses.First(l => string.Equals(l.Lens, lens, StringComparison.Ordinal)).Version;
        return new ItemScope(
            anchors,
            Common(_lenses.Select(static l => l.Version)),
            new ScopeMapVersions(Version("code"), Version("product"), Version("related"), Version("history")),
            Common(_lenses.Select(static l => l.Commit)),
            packetHash,
            frozenAt);
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string? Common(IEnumerable<string> values)
    {
        var distinct = values.Distinct(StringComparer.Ordinal).ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }

    private static (string Version, string Commit)? Split(string? mapVersion)
    {
        var at = mapVersion?.IndexOf('@', StringComparison.Ordinal) ?? -1;
        if (mapVersion is null || at < 2 || at == mapVersion.Length - 1 || mapVersion[0] != 'v' || !mapVersion.AsSpan(1, at - 1).ToString().All(char.IsAsciiDigit))
        {
            return null;
        }

        return (mapVersion[..at], mapVersion[(at + 1)..]);
    }
}
