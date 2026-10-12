using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// The query pipeline (spec §8.2) for Phase 1's text shape: scope and gate, terms, every source searched under the
/// deadline (a missing source is degraded; all of them missing is SND-CONTEXT-011), canonical dedup and RRF, the top
/// items fetched and cleaned once at the ingest boundary, one batched version check per source, then packing,
/// citations and per-field egress scrubbing (<see cref="ResultAssembly"/>). No model is called.
/// </summary>
internal sealed class QueryContextService(ISourceCatalog catalog, IIngestFilter ingest, IEgressScrubber egress, IIdGenerator ids, TimeProvider clock)
    : IService<QueryContextRequest, ContextResult>
{
    /// <summary>The most fused items fetched (and so cited) per query.</summary>
    internal const int MaxItems = 20;

    /// <summary>The most text kept from one record after cleaning (spec §8.3: 64 KB).</summary>
    internal const int MaxRecordText = 64 * 1024;

    public async ValueTask<Result<ContextResult>> Handle(QueryContextRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var repos = settings.Bind(s => ScopeRules.Resolve(s, request.Scope.Repos, request.Scope.Caller));
        var valid = repos.Bind(_ => QueryRules.Check(request.Query, settings.Value));
        if (valid.IsError)
        {
            return valid.Error;
        }

        var deadline = clock.GetUtcNow() + settings.Value.Deadline;
        var sources = catalog.For(settings.Value, repos.Value).Where(s => ScopeRules.MayRead(request.Scope.Caller, s.Describe())).ToList();
        var searched = await Search(sources, valid.Value.Terms, deadline, cancellationToken).ConfigureAwait(false);
        if (sources.Count > 0 && searched.Answers.Count == 0)
        {
            return ContextErrors.AllSourcesUnavailable.With(
                $"no source answered ({string.Join(", ", searched.Degraded.Select(d => d.Source.Value))}); fix: sindri context sources status");
        }

        var degraded = searched.Degraded.ToList();
        var fused = RankFusion.Fuse(searched.Answers).Take(MaxItems).ToList();
        var fetched = await Fetch(fused, sources, deadline, degraded, cancellationToken).ConfigureAwait(false);
        var versions = await Versions(fetched, sources, deadline, cancellationToken).ConfigureAwait(false);
        var assembly = new ResultAssembly(egress, QueryId.From("q_" + ids.NewId().ToString("N", CultureInfo.InvariantCulture)), clock.GetUtcNow());
        return assembly.Build(request.Query, valid.Value.Budget, fetched, versions, degraded);
    }

    private async Task<Searched> Search(IReadOnlyList<IContextSource> sources, IReadOnlyList<string> terms, DateTimeOffset deadline, CancellationToken ct)
    {
        var calls = sources.Select(async source =>
        {
            var query = new SourceQuery(terms, AsOf: null, Limit: 50, deadline - clock.GetUtcNow());
            var hits = await SourceCalls.Within(source, query.Deadline, t => source.Search(query, t), ct).ConfigureAwait(false);
            return (source.Describe().Id, hits);
        });
        var answers = new List<SourceAnswer>();
        var degraded = new List<DegradedSource>();
        foreach (var (id, hits) in await Task.WhenAll(calls).ConfigureAwait(false))
        {
            if (hits.IsSuccess)
            {
                answers.Add(new SourceAnswer(id, hits.Value));
            }
            else
            {
                degraded.Add(SourceCalls.Degraded(id, hits.Error, clock.GetUtcNow()));
            }
        }

        return new Searched(answers, degraded);
    }

    private async Task<IReadOnlyList<FetchedRecord>> Fetch(IReadOnlyList<FusedItem> items, IReadOnlyList<IContextSource> sources, DateTimeOffset deadline,
        List<DegradedSource> degraded, CancellationToken ct)
    {
        var fetched = new List<FetchedRecord>();
        foreach (var item in items)
        {
            if (deadline - clock.GetUtcNow() <= TimeSpan.Zero)
            {
                break;   // no claim without a citation: what wasn't fetched in time isn't shown (search already answered)
            }

            var hit = item.Hits[0];
            var source = sources.First(s => s.Describe().Id == hit.Ref.Source);
            var record = await SourceCalls.Within(source, deadline - clock.GetUtcNow(), t => source.Fetch(hit.Ref, t), ct).ConfigureAwait(false);
            if (record.IsError)
            {
                if (record.Error.Kind != ErrorKind.NotFound && degraded.TrueForAll(d => d.Source != hit.Ref.Source))
                {
                    degraded.Add(SourceCalls.Degraded(hit.Ref.Source, record.Error, clock.GetUtcNow()));
                }

                continue;   // a record gone since the search, or a source that stopped answering: no claim without a citation
            }

            var text = record.Value.Blocked   // the adapter's own whole-document clean withheld it: cite it as scrub-blocked
                ? new ScrubbedField(string.Empty, Redacted: false, Blocked: true)
                : ingest.Clean(record.Value.Text, singleLine: false);   // cleaned whole, then cut, so a cut never splits a secret
            var cut = text.Text.Length > MaxRecordText ? text with { Text = text.Text[..MaxRecordText] } : text;
            fetched.Add(new FetchedRecord(item, source, ingest.Clean(record.Value.Title, singleLine: true), cut));
        }

        return fetched;
    }

    /// <summary>One batched version check per source, under what is left of the deadline (spec §8.2 step 8).</summary>
    private async Task<IReadOnlyDictionary<SourceRef, (bool Ran, SourceVersion? Current)>> Versions(IReadOnlyList<FetchedRecord> fetched,
        IReadOnlyList<IContextSource> sources, DateTimeOffset deadline, CancellationToken ct)
    {
        var checkedRefs = new Dictionary<SourceRef, (bool, SourceVersion?)>();
        foreach (var group in fetched.SelectMany(f => f.Item.Hits).Select(h => h.Ref).Distinct().GroupBy(r => r.Source))
        {
            var source = sources.First(s => s.Describe().Id == group.Key);   // each ref is checked by its own source
            var refs = group.ToList();
            var left = deadline - clock.GetUtcNow();
            var versions = left <= TimeSpan.Zero
                ? ContextErrors.SourceUnavailable.With("no time left for the version check")
                : await SourceCalls.Within(source, left, t => source.Versions(refs, t), ct).ConfigureAwait(false);
            for (var i = 0; i < refs.Count; i++)
            {
                checkedRefs[refs[i]] = versions.IsSuccess && versions.Value.Count == refs.Count ? (true, versions.Value[i]) : (false, null);
            }
        }

        return checkedRefs;
    }
}
