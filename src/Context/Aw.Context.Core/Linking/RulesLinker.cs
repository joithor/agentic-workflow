using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linking;

/// <summary>
/// The rules-first linker (spec §13): issue keys and URLs, repo paths, identifier tokens and quoted titles in a record's scrubbed text, matched to the map's nodes and to other records.
/// Pure and idempotent: the same text and graph give the same links. Plain English words never link.
/// </summary>
public static class RulesLinker
{
    /// <summary>Links one record's text.</summary>
    /// <param name="record">The record the text belongs to (it never links to itself).</param>
    /// <param name="text">The sanitized, scrubbed text.</param>
    /// <param name="graph">The map to link into.</param>
    /// <param name="known">Every intent record the sync knows, for the issue-key and URL rules.</param>
    /// <param name="teams">The profile's tracker teams: only their keys link.</param>
    /// <returns>The links, ordered by target then rule.</returns>
    public static IReadOnlyList<IntentLink> Link(RecordKey record, string text, GraphSnapshot graph, IReadOnlyList<IntentRecord> known, IReadOnlySet<string> teams) =>
        throw new NotSupportedException("S-5B slice 1: the linker is not written yet");
}
