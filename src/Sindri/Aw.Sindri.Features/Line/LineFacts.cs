using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>The facts a line Step's code reads: JSON trees by key (without the 'fact:' prefix), parsed once by the runtime.</summary>
/// <param name="nodes">The parsed facts.</param>
internal sealed class LineFacts(IReadOnlyDictionary<string, JsonNode?> nodes)
{
    /// <summary>The fact's tree, or null when it is absent or JSON null.</summary>
    internal JsonNode? this[string key] => nodes.GetValueOrDefault(key);

    /// <summary>Whether the fact is present and not JSON null.</summary>
    internal bool Has(string key) => this[key] is not null;
}
