using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linking;

/// <summary>A deterministic link from an intent record to a node, with the hash of the span that justified it (never the text, spec §7).</summary>
/// <param name="Record">The record the text belongs to.</param>
/// <param name="Target">The node (a map node, or another record's 'record:' node) it links to.</param>
/// <param name="Rule">The rule that produced it.</param>
/// <param name="Confidence">0 to 1, by rule.</param>
/// <param name="SpanHash">The lower-case hex SHA-256 of the matched span.</param>
public sealed record IntentLink(RecordKey Record, NodeKey Target, LinkRule Rule, double Confidence, string SpanHash);
