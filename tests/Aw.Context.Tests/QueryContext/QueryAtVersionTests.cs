using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.QueryContext;

/// <summary>A node question as of a retained map version (scoping spec C10): the graph sections come from that version, the sources stay live.</summary>
public sealed class QueryAtVersionTests : IDisposable
{
    private const string Manager = "feature:tabs.tab-manager";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task TabsThenOther()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);   // v1
        _world.Clock.Advance(TimeSpan.FromDays(3));
        _ = await _world.ImportYaml(MapWorld.Yaml("schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:", "  - {key: \"feature:only-in-two\", label: \"Two\"}"));   // v2
    }

    private ValueTask<Kernel.Result<ContextResult>> Ask(ContextQuery query) => _world.Engine.Query(query, MapWorld.Human, MapWorld.None);

    [Fact, Trait("Lane", "unit")]
    public async Task Query_node_at_mapVersion_expands_on_that_version()
    {
        await TabsThenOther();

        var old = await Ask(new ContextQuery(Text: null, Node: Manager) { MapVersion = "v1" });
        var current = await Ask(new ContextQuery(Text: null, Node: Manager));

        Assert.Equal("v1", old.Value.MapVersion);
        Assert.NotEmpty(old.Value.Anchors);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, current.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Query_at_unretained_version_is_120_and_a_text_question_does_not_swallow_it()
    {
        await TabsThenOther();

        var node = await Ask(new ContextQuery(Text: null, Node: Manager) { MapVersion = "v99" });
        var text = await Ask(new ContextQuery("tab manager") { MapVersion = "v99" });

        Assert.Equal((ContextErrors.MapVersionNotRetained.Code, ContextErrors.MapVersionNotRetained.Code), (node.Error.Code, text.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Query_draft_together_with_mapVersion_or_a_malformed_one_is_006()
    {
        await TabsThenOther();

        var both = await Ask(new ContextQuery(Text: null, Node: Manager, Draft: true) { MapVersion = "v1" });
        var malformed = await Ask(new ContextQuery(Text: null, Node: Manager) { MapVersion = "latest" });

        Assert.Equal((ContextErrors.QueryInvalid.Code, ContextErrors.QueryInvalid.Code), (both.Error.Code, malformed.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_maps_citation_says_when_the_version_was_written()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);
        var written = _world.Clock.GetUtcNow();
        _world.Clock.Advance(TimeSpan.FromDays(2));

        var result = await Ask(new ContextQuery(Text: null, Node: Manager));

        Assert.Equal(written, result.Value.Cites["C1"].Refs[0].FetchedAt);
    }
}
