using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Graph;

/// <summary>One piece of evidence for a node or edge: the record's canonical key and, for a quote, the span's hash.</summary>
/// <param name="Key">The record's canonical key, such as 'repo:aw-split-panes/src/layout/SplitPane.ts'.</param>
/// <param name="Span">The cited span's hash (or a line range), or null for the whole record.</param>
public sealed record EvidenceRef(RecordKey Key, string? Span = null);
