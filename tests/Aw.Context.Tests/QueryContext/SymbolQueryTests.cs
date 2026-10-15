using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.QueryContext;

/// <summary>The symbol shape in its file-path form (spec Phase 4): a file resolves to the features it implements.</summary>
public sealed class SymbolQueryTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_path_resolves_to_the_features_it_implements_with_an_affects_section()
    {
        _ = await _world.ImportFixture();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Symbol: "src/layout/SplitPane.ts"), MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(["feature:layout.split-pane", "feature:layout.split-pane.orientation"], result.Anchors, StringComparer.Ordinal);
        Assert.Contains(result.Sections, s => string.Equals(s.Name, "affects", StringComparison.Ordinal));
        Assert.Contains(result.Sections.Single(s => string.Equals(s.Name, "code", StringComparison.Ordinal)).Items, i => string.Equals(i.Node, "code:src/layout/SplitPane.ts", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("code:src/layout/SplitPane.ts")]
    [InlineData(" src/layout/SplitPane.ts ")]
    [InlineData("./src/layout/SplitPane.ts")]
    [InlineData("/src/layout/SplitPane.ts")]
    [InlineData("src//layout/SplitPane.ts")]
    public async Task The_code_prefix_and_surrounding_space_are_accepted(string symbol)
    {
        _ = await _world.ImportFixture();

        var result = await _world.Engine.Query(new ContextQuery(Text: null, Symbol: symbol), MapWorld.Human, MapWorld.None);

        Assert.Equal(2, result.Value.Anchors.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/nope.ts", "SND-CONTEXT-003")]
    [InlineData("src/layout/SplitPane.ts#render", "SND-CONTEXT-003")]
    [InlineData("README.md", "SND-CONTEXT-003")]
    [InlineData("", "SND-CONTEXT-006")]
    public async Task An_unknown_file_a_symbol_level_anchor_or_an_empty_path_is_refused(string symbol, string code)
    {
        _ = await _world.ImportFixture();

        var result = await _world.Engine.Query(new ContextQuery(Text: null, Symbol: symbol), MapWorld.Human, MapWorld.None);

        Assert.Equal(code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_map_a_symbol_query_is_SND_CONTEXT_030()
    {
        var result = await _world.Engine.Query(new ContextQuery(Text: null, Symbol: "src/layout/SplitPane.ts"), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoMap.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_in_the_files_own_repo_resolves_it_to_the_features_it_implements()
    {
        _ = await _world.ImportFixture();

        var result = await _world.Engine.Query(new ContextQuery(Text: null, Symbol: "src/layout/SplitPane.ts"), MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : string.Empty);
        Assert.Equal(["feature:layout.split-pane", "feature:layout.split-pane.orientation"], result.Value.Anchors, StringComparer.Ordinal);
        Assert.Contains(result.Value.Sections.Single(s => string.Equals(s.Name, "code", StringComparison.Ordinal)).Items, i => string.Equals(i.Node, "code:src/layout/SplitPane.ts", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_outside_the_files_repo_scope_gets_003_like_any_unknown_anchor()
    {
        _ = await _world.ImportFixture();

        var result = await _world.Engine.Query(new ContextQuery(Text: null, Symbol: "src/layout/SplitPane.ts"), MapWorld.Session("other"), MapWorld.None);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, result.Error.Code);
    }
}
