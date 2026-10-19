using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>
/// The scoping station's gold facts (tests/fixtures/scoping/facts/expiry): the line gold scope packet's files as the facts a Step
/// directory holds, plus the markers SC-K's rules need. Embedded, so no test reads a file.
/// </summary>
public sealed class ScopingFacts : Dictionary<string, string>
{
    /// <summary>An empty fact set with ordinal keys.</summary>
    public ScopingFacts()
        : base(StringComparer.Ordinal)
    {
    }

    private const string Prefix = "scoping-facts/";

    /// <summary>A fresh, editable copy of the expiry gold facts: JSON text by fact key (no 'fact:' prefix).</summary>
    /// <returns>The facts.</returns>
    public static ScopingFacts Expiry()
    {
        var facts = new ScopingFacts();
        foreach (var name in typeof(ScopingFacts).Assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix + "expiry/", StringComparison.Ordinal)))
        {
            facts[Path.GetFileNameWithoutExtension(name)] = Read(name);
        }

        return facts;
    }

    /// <summary>A context view fixture's JSON text ('lensesView', 'codeView', 'historyView', 'contextQuery' or 'proposeResult').</summary>
    /// <param name="name">The fixture's file name without its extension.</param>
    /// <returns>The JSON text.</returns>
    public static string View(string name) => Read("scoping-views/" + name + ".json");

    /// <summary>Edits one fact's JSON in place.</summary>
    /// <param name="key">The fact.</param>
    /// <param name="edit">Receives the fact's parsed JSON; change it in place.</param>
    /// <returns>The same facts, for chaining.</returns>
    public ScopingFacts Edit(string key, Action<JsonNode> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var node = JsonNode.Parse(this[key])!;
        edit(node);
        this[key] = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return this;
    }

    /// <summary>Removes one fact, as an agent's Step directory that does not hold it.</summary>
    /// <param name="key">The fact.</param>
    /// <returns>The same facts, for chaining.</returns>
    public ScopingFacts Without(string key)
    {
        _ = Remove(key);
        return this;
    }

    private static string Read(string resource)
    {
        using var stream = typeof(ScopingFacts).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
