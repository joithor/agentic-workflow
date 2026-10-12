using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.QueryContext;

public sealed class QueryContextTests
{
    private static readonly SourceDescriptor _onyx = new(SourceId.From("onyx"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false);

    [Fact, Trait("Lane", "unit")]
    public async Task QueryContext_returns_cited_versioned_repo_doc_records()
    {
        var result = await Engines.Over(FakeContextSource.FixtureDocs()).Query(new ContextQuery("orientation toggle"), Engines.Human(), CancellationToken.None);

        var records = result.Value.Sections.Single();
        Assert.Equal("records", records.Name);
        var top = records.Items[0];
        Assert.Equal("0001 Orientation toggle", top.Title);
        Assert.StartsWith("<untrusted source=\"repo-docs\">", top.Text, StringComparison.Ordinal);
        Assert.Equal((Trust.Untrusted, Provenance.Deterministic, false), (top.Trust, top.Provenance, top.Confirmed));
        var cite = result.Value.Cites[top.Cites.Single()];
        Assert.Equal("repo:split-panes/docs/decisions/0001-orientation-toggle.md", cite.Key.Value);
        Assert.Equal((CitationState.Current, "b2"), (cite.State, cite.Refs.Single().Version.Value));
        Assert.Equal(["rrf:repo-docs#1"], top.Why, StringComparer.Ordinal);
        Assert.Empty(result.Value.Degraded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_source_is_degraded_and_the_others_still_answer()
    {
        var onyx = new FakeContextSource(_onyx, []) { Failure = ContextErrors.SourceUnavailable.With("Onyx did not answer") };

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), onyx).Query(new ContextQuery("orientation"), Engines.Human(), CancellationToken.None);

        var degraded = result.Value.Degraded.Single();
        Assert.Equal(("onyx", "SND-CONTEXT-010", "Onyx did not answer"), (degraded.Source.Value, degraded.Code, degraded.Message));
        Assert.NotEmpty(result.Value.Sections.Single().Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_source_down_is_011_not_an_empty_answer()
    {
        var docs = FakeContextSource.FixtureDocs();
        docs.Failure = ContextErrors.SourceUnavailable.With("down");

        var result = await Engines.Over(docs).Query(new ContextQuery("orientation"), Engines.Human(), CancellationToken.None);

        Assert.Equal(ContextErrors.AllSourcesUnavailable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_past_the_deadline_is_degraded()
    {
        var slow = new FakeContextSource(_onyx, []) { Hangs = true };

        var result = await Engines.Over(FakeContextSource.FixtureDocs(), slow)
            .Query(new ContextQuery("orientation"), Engines.Human("""{"deadline":"200ms"}"""), CancellationToken.None);

        Assert.Equal("onyx", result.Value.Degraded.Single().Source.Value);
        Assert.Contains("deadline", result.Value.Degraded.Single().Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_that_moved_on_is_cited_stale_with_both_versions()
    {
        var docs = FakeContextSource.FixtureDocs();
        docs.MoveOn(ExternalId.From("split-panes/docs/decisions/0001-orientation-toggle.md"), SourceVersion.From("c3"));

        var result = await Engines.Over(docs).Query(new ContextQuery("orientation toggle"), Engines.Human(), CancellationToken.None);

        var cite = result.Value.Cites["C1"];
        Assert.Equal(CitationState.Stale, cite.State);
        Assert.Equal(("b2", "c3"), (cite.Refs.Single().Version.Value, cite.Refs.Single().Current?.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_version_check_that_fails_leaves_the_citation_unchecked()
    {
        var docs = FakeContextSource.FixtureDocs();
        docs.VersionsFail = true;

        var result = await Engines.Over(docs).Query(new ContextQuery("orientation"), Engines.Human(), CancellationToken.None);

        Assert.All(result.Value.Cites.Values, c => Assert.Equal(CitationState.Unchecked, c.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Secrets_are_redacted_and_a_blocked_record_keeps_its_citation_without_text()
    {
        var docs = new FakeContextSource(FakeContextSource.FixtureDocs().Describe(),
        [
            FakeContextSource.Doc("docs/a.md", "Keys " + FakeScrubber.Secret, "orientation key " + FakeScrubber.Secret, "v1"),
            FakeContextSource.Doc("docs/b.md", "Blocked", "orientation " + FakeScrubber.BlockMe, "v2"),
        ]);

        var result = await Engines.Over(docs).Query(new ContextQuery("orientation"), Engines.Human(), CancellationToken.None);

        var items = result.Value.Sections.Single().Items;
        Assert.DoesNotContain(items, i => (i.Title + i.Text).Contains(FakeScrubber.Secret, StringComparison.Ordinal));
        Assert.Contains(items, i => i.Text is not null && i.Text.Contains("[REDACTED:fake]", StringComparison.Ordinal));
        var blocked = items.Single(i => i.Title == "Blocked");
        Assert.Null(blocked.Text);
        Assert.Equal(CitationState.ScrubBlocked, result.Value.Cites[blocked.Cites.Single()].State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Items_past_the_budget_are_listed_as_references_only()
    {
        var result = await Engines.Over(FakeContextSource.FixtureDocs()).Query(new ContextQuery("layout orientation", Budget: 60), Engines.Human(), CancellationToken.None);

        var records = result.Value.Sections.Single();
        Assert.True(result.Value.Truncated);
        Assert.Equal(2, records.Items.Count + records.More.Count);
        Assert.NotEmpty(records.More);
        Assert.True(result.Value.Budget.Used <= 60);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_is_gated_and_scoped_to_its_repo()
    {
        var catalog = new FixedSourceCatalog(FakeContextSource.FixtureDocs());
        var engine = Engines.Over(catalog);

        var off = await engine.Query(new ContextQuery("orientation"), Engines.Session("split-panes"), CancellationToken.None);
        var on = await engine.Query(new ContextQuery("orientation"), Engines.Session("split-panes", """{"sessions":{"enabled":true}}"""), CancellationToken.None);
        var elsewhere = await engine.Query(new ContextQuery("orientation"), Engines.Session("nope", """{"sessions":{"enabled":true}}"""), CancellationToken.None);

        Assert.Equal(ContextErrors.SessionsDisabled.Code, off.Error.Code);
        Assert.True(on.IsSuccess);
        Assert.Equal(["split-panes"], catalog.LastRepos.Select(r => r.Name), StringComparer.Ordinal);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, elsewhere.Error.Code);
    }
}
