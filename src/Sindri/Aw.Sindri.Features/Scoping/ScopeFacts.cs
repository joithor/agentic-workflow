using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The facts a check, view or Step reads: JSON text by key (without the 'fact:' prefix), parsed once. A fact that is not JSON is present and unparseable, which the schema rule reports.</summary>
internal sealed class ScopeFacts
{
    private readonly Dictionary<string, JsonNode?> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _unparseable = new(StringComparer.Ordinal);

    internal ScopeFacts(IReadOnlyDictionary<string, string> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var (key, body) in text)
        {
            try
            {
                _nodes[key] = JsonNode.Parse(body);
            }
            catch (JsonException e)
            {
                _unparseable[key] = e.Message;   // boundary: a fact is text a model or a person wrote
            }
        }
    }

    /// <summary>The facts that are present but are not JSON, with the parser's reason.</summary>
    internal IReadOnlyDictionary<string, string> Unparseable => _unparseable;

    /// <summary>Whether the fact is present and holds a value (JSON null counts as absent).</summary>
    internal bool Has(string key) => _nodes.GetValueOrDefault(key) is not null;

    /// <summary>The fact's tree, or null when absent.</summary>
    internal JsonNode? this[string key] => _nodes.GetValueOrDefault(key);

    /// <summary>The fact's tree for a caller that checked it is present; an empty object when it is not.</summary>
    internal JsonNode Require(string key) => _nodes.GetValueOrDefault(key) ?? new JsonObject();

    /// <summary>A deep copy of an object fact, for a caller that edits it; an empty object when the fact is absent or is not an object.</summary>
    internal JsonObject ObjectCopy(string key) => _nodes.GetValueOrDefault(key) is JsonObject obj ? (JsonObject)obj.DeepClone() : [];

    /// <summary>A copy with one fact replaced (the original is unchanged).</summary>
    internal ScopeFacts With(string key, JsonNode node)
    {
        var copy = new ScopeFacts(new Dictionary<string, string>(StringComparer.Ordinal));
        foreach (var (k, v) in _nodes)
        {
            copy._nodes[k] = v;
        }

        foreach (var (k, v) in _unparseable)
        {
            copy._unparseable[k] = v;
        }

        _ = copy._unparseable.Remove(key);
        copy._nodes[key] = node;
        return copy;
    }

    /// <summary>The keys among <paramref name="keys"/> the facts do not hold, in the order given.</summary>
    internal IReadOnlyList<string> Missing(params string[] keys) => [.. keys.Where(k => !Has(k) && !_unparseable.ContainsKey(k))];
}
