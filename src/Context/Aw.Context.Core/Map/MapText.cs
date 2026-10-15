using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Map;

/// <summary>
/// The map's text as answers write it (spec §7): every label, wording and quote crosses this call's ingest boundary
/// (sanitize, then the built-in rules and the profile's extra patterns), then the egress scrubber, one field at a time.
/// Labels are one line of at most 200 characters; untrusted text (model output not yet accepted, quotes) goes inside the
/// fence. The map's text is cleaned on the way out with the current rules, so a change to the patterns applies to the
/// next answer; nothing cleaned is stored, so there is no ruleset to record.
/// </summary>
/// <param name="scrubbers">This call's scrubbers.</param>
public sealed class MapText(Scrubbers scrubbers)
{
    private const string Blocked = "[REDACTED:blocked]";
    private const string Source = "map";

    /// <summary>A label: one line, at most 200 characters, fenced when untrusted.</summary>
    /// <param name="label">The label as stored.</param>
    /// <param name="trust">Its trust.</param>
    /// <returns>The text to write.</returns>
    public string Label(string label, Results.Trust trust) => Fenced(UntrustedFence.Cap(Clean(label, singleLine: true)), trust);

    /// <summary>A longer text (a quote), fenced when untrusted.</summary>
    /// <param name="text">The text as stored.</param>
    /// <param name="trust">Its trust.</param>
    /// <returns>The text to write.</returns>
    public string Body(string text, Results.Trust trust) => Fenced(Clean(text, singleLine: false), trust);

    /// <summary>A line of keys and edge names: egress only (no markup to sanitize), never fenced.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The scrubbed line.</returns>
    public string Line(string line) => scrubbers.Egress.Scrub(line) is { Blocked: false } scrubbed ? scrubbed.Text : Blocked;

    /// <summary>A node as an answer shows it: its label written by <see cref="Label"/>, confirmed when human, trust from provenance.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The fact.</returns>
    public NodeFact Fact(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var confirmed = node.Provenance == Provenance.Human;
        var trust = Graph.TrustPolicy.For(node.Provenance, confirmed);
        return new NodeFact(node.Key, node.Key.Kind, Label(node.Label, trust), node.Provenance, confirmed, trust);
    }

    private string Clean(string text, bool singleLine)
    {
        var ingested = scrubbers.Ingest.Clean(text, singleLine);
        return ingested.Blocked || scrubbers.Egress.Scrub(ingested.Text) is not { Blocked: false } scrubbed ? Blocked : scrubbed.Text;
    }

    private static string Fenced(string text, Results.Trust trust) => trust == Results.Trust.Untrusted ? UntrustedFence.Wrap(Source, author: null, text) : text;
}
