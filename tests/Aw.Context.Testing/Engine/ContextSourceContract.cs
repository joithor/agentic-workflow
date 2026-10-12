using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="IContextSource"/> honours (spec §10), run against the fake and every real adapter
/// alike. A derived test class binds <see cref="Create"/> to one implementation over the split-panes fixture's docs
/// (which hold 'orientation' and 'layout') and carries its lane.
/// </summary>
/// <typeparam name="TSource">The implementation.</typeparam>
public abstract class ContextSourceContract<TSource>
    where TSource : IContextSource
{
    private static readonly SourceQuery _orientation = new(["orientation"], AsOf: null, Limit: 50, TimeSpan.FromSeconds(10));

    /// <summary>A fresh source over the fixture's docs.</summary>
    /// <returns>The source.</returns>
    protected abstract Task<TSource> Create();

    /// <summary>Hits are ranked 1, 2, … in order, at most the limit, each carrying a canonical key and a version.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Search_ranks_from_one_within_the_limit()
    {
        var source = await Create();

        var hits = Expect(await source.Search(_orientation with { Terms = ["layout", "orientation"], Limit = 1 }, CancellationToken.None));
        var all = Expect(await source.Search(_orientation, CancellationToken.None));

        Check(hits.Count == 1, "a limit of 1 returns one hit");
        Check(all.Select(h => h.Rank).SequenceEqual(Enumerable.Range(1, all.Count)), "ranks run 1, 2, … in order");
        Check(all.All(h => h.Ref.Source == source.Describe().Id), "every hit names this source");
    }

    /// <summary>A term the docs don't hold finds nothing (and is never read as query syntax).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Search_without_a_match_is_empty_even_for_syntax()
    {
        var source = await Create();

        var none = Expect(await source.Search(_orientation with { Terms = ["zzqx", "\" OR 1 OR \"", "NEAR(", "*"] }, CancellationToken.None));

        Check(none.Count == 0, "no hit for terms the docs don't hold");
    }

    /// <summary>Fetching a hit returns its record at the same reference, with text.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Fetch_returns_the_hit_s_record()
    {
        var source = await Create();
        var hit = Expect(await source.Search(_orientation, CancellationToken.None))[0];

        var record = Expect(await source.Fetch(hit.Ref, CancellationToken.None));

        Check(record.Ref == hit.Ref, "the record carries the hit's reference");
        Check(record.Text.Contains("rientation", StringComparison.Ordinal), "its text holds the term");
    }

    /// <summary>A version that doesn't exist is NotFound, not an exception.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Fetch_of_a_missing_version_is_not_found()
    {
        var source = await Create();
        var hit = Expect(await source.Search(_orientation, CancellationToken.None))[0];

        var missing = await source.Fetch(hit.Ref with { Version = SourceVersion.From("0000000000000000000000000000000000000000") }, CancellationToken.None);

        Check(missing.IsError && missing.Error.Kind == ErrorKind.NotFound, "a missing version is NotFound");
    }

    /// <summary>Versions answers one entry per reference, in order, and an unchanged record is at its cited version.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Versions_are_batched_and_in_order()
    {
        var source = await Create();
        var hits = Expect(await source.Search(_orientation with { Terms = ["layout", "orientation"] }, CancellationToken.None));

        var versions = Expect(await source.Versions([.. hits.Select(h => h.Ref)], CancellationToken.None));

        Check(versions.Count == hits.Count, "one version per reference");
        Check(hits.Select(h => (SourceVersion?)h.Ref.Version).SequenceEqual(versions), "unchanged records are at their cited versions");
    }

    /// <summary>A healthy source probes ok.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Probe_is_ok()
    {
        var source = await Create();

        Check((await source.Probe(CancellationToken.None)).State == SourceState.Ok, "a healthy source probes ok");
    }

    private static T Expect<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"{typeof(TSource).Name}: {result.Error.Code} {result.Error.Message}");

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TSource).Name}: {what}");
        }
    }
}
