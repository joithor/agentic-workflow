using Aw.Context.Core;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.FindPath;

public sealed class FindPathTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task The_path_from_orientation_to_the_tab_manager_is_found_with_its_steps()
    {
        _ = await _world.ImportFixture();

        var view = (await _world.Engine.Path("feature:layout.orientation", "Tabs", MapWorld.Human, MapWorld.None)).Value;

        Assert.True(view.Found);
        Assert.Equal(("feature:layout.split-pane.orientation", "feature:tabs.tab-manager"), (view.From.Value, view.To.Value));
        Assert.Equal(6, view.Steps.Count);
        Assert.Equal("Tabs", view.Steps[^1].Node.Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_path_is_an_answer_not_an_error()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1",
            "kind: authored",
            "repo: aw-split-panes",
            "nodes:",
            "  - {key: \"feature:a\", label: \"A\"}",
            "  - {key: \"feature:b\", label: \"B\"}"));

        var view = (await _world.Engine.Path("feature:a", "feature:b", MapWorld.Human, MapWorld.None)).Value;

        Assert.False(view.Found);
        Assert.Empty(view.Steps);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_end_is_SND_CONTEXT_003()
    {
        _ = await _world.ImportFixture();

        var view = await _world.Engine.Path("feature:layout.split-pane", "feature:nope", MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, view.Error.Code);
    }
}
