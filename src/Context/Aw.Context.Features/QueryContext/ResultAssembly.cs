using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Packing;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// Turns fetched records into the result (spec §8.3, §8.4): one 'record' item and one citation per record, every field
/// scrubbed on its own before it is written (a blocked field is '[REDACTED:blocked]' in that field only), untrusted
/// text inside the fence, then packed into the budget.
/// </summary>
/// <param name="egress">The per-field scrubber.</param>
/// <param name="queryId">The query's id.</param>
/// <param name="asOf">When the answer is assembled.</param>
internal sealed class ResultAssembly(IEgressScrubber egress, QueryId queryId, DateTimeOffset asOf)
{
    internal ContextResult Build(ContextQuery query, int budget, IReadOnlyList<FetchedRecord> fetched,
        IReadOnlyDictionary<SourceRef, (bool Ran, SourceVersion? Current)> versions, IReadOnlyList<DegradedSource> degraded)
    {
        var cites = new Dictionary<string, Citation>(StringComparer.Ordinal);
        var candidates = new List<PackCandidate>();
        foreach (var record in fetched)
        {
            var marker = "C" + (cites.Count + 1).ToString(CultureInfo.InvariantCulture);
            cites[marker] = Cite(record, versions);
            candidates.Add(new PackCandidate("records", Item(record, marker), marker));
        }

        var packed = Packer.Pack(candidates, budget);
        return new ContextResult(ContextResult.CurrentSchemaVersion, queryId, query, Anchors: [], MapVersion: null, asOf, packed.Sections,
            Conflicts: [], SuggestedAnchors: [], degraded, packed.Truncated, new BudgetUse(budget, packed.Used, Estimate: true), cites);
    }

    private ResultItem Item(FetchedRecord record, string marker)
    {
        var source = record.Source.Describe().Id.Value;
        var title = UntrustedFence.Wrap(source, author: null, UntrustedFence.Cap(egress.Scrub(record.Title.Text).Text));   // titles are record text too (§7)
        var text = record.Text.Blocked ? null : UntrustedFence.Wrap(source, author: null, egress.Scrub(record.Text.Text).Text);
        return new ResultItem("record", Node: null, title, text, Math.Round(record.Item.Score, 6, MidpointRounding.ToEven), record.Item.Why, Provenance.Deterministic,
            Confirmed: false, Trust.Untrusted, [marker])
        { Tokens = TokenEstimate.Of(title) + TokenEstimate.Of(text) };
    }

    private Citation Cite(FetchedRecord record, IReadOnlyDictionary<SourceRef, (bool Ran, SourceVersion? Current)> versions)
    {
        var refs = new List<CitationRef>();
        var states = new List<CitationState>();
        foreach (var hit in record.Item.Hits)
        {
            var (state, current) = CitationStates.Of(hit.Ref.Version, versions.GetValueOrDefault(hit.Ref, (false, null)), record.Text.Blocked);
            states.Add(state);
            refs.Add(new CitationRef(hit.Ref.Source, ExternalId.From(Scrubbed(hit.Ref.Id.Value)), hit.Ref.Version) { Current = current });
        }

        var best = record.Item.Hits[0];
        return new Citation(RecordKey.From(Scrubbed(record.Item.Key.Value)), refs, Permalinks.Allowed(best.Ref.Permalink), Trust.Untrusted, Worst(states));
    }

    /// <summary>An id or key scrubbed like any other field; a blocked one keeps a fixed, valid placeholder.</summary>
    private string Scrubbed(string value) => egress.Scrub(value) is { Blocked: false } scrubbed ? scrubbed.Text : "scrub:blocked";

    /// <summary>The citation's state is its worst reference's: blocked, then stale, then unchecked, then current.</summary>
    private static CitationState Worst(List<CitationState> states) =>
        new[] { CitationState.ScrubBlocked, CitationState.Stale, CitationState.Unchecked, CitationState.Unstable }.FirstOrDefault(states.Contains, CitationState.Current);
}
