using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Symbols;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Map;
using Aw.Context.Testing.Symbols;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetLenses;

/// <summary>Phase 9: the lenses' 'coverage.code.level' is 'symbol' when the code index answers for the code band's files, else it stays 'file'.</summary>
public sealed class LensesSymbolCoverageTests
{
    private const string Anchor = "feature:tabs.tab-manager";
    private static readonly ContextRepoName _repo = ContextRepoName.From(FixtureMap.Repo);

    private static readonly Dictionary<string, string> _tree = new(StringComparer.Ordinal)
    {
        ["src/tabs/TabManager.ts"] = "export class TabManager {}",
        ["src/tabs/TabGroups.ts"] = "export const b = 1;",
    };

    private static readonly SymbolSite _manager = new("src/tabs/TabManager.ts#TabManager", "TabManager", "class", "src/tabs/TabManager.ts", 1, 9, Signature: null, Exported: true);

    private static async Task<LensesView> Ask(FakeIndexedRepo? index)
    {
        using var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, _tree), symbols: new FakeCodeSymbolSource(index is null ? [] : [index]));
        _ = await world.ImportYaml(string.Concat("{\"commit\": \"", MapWorld.Head, "\", ", MapWorld.TabsMap.TrimStart().AsSpan(1)));
        return (await world.Engine.Lenses(Anchor, MapRead.Current, depth: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None)).Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_that_answers_makes_the_code_coverage_symbol_level()
    {
        var view = await Ask(new FakeIndexedRepo(_repo, CodeIndexState.Ok, MapWorld.Head, [_manager], []));

        Assert.Equal((SymbolsState.Ok, "symbol"), (view.Bands.Code.Symbols.State, view.Coverage.Code.Level));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_index_still_answers_so_the_level_is_symbol()
    {
        var view = await Ask(new FakeIndexedRepo(_repo, CodeIndexState.Ok, "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293", [_manager], []));

        Assert.Equal((SymbolsState.Stale, "symbol"), (view.Bands.Code.Symbols.State, view.Coverage.Code.Level));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_index_keeps_the_level_at_file()
    {
        var view = await Ask(index: null);

        Assert.Equal((SymbolsState.Unavailable, "file"), (view.Bands.Code.Symbols.State, view.Coverage.Code.Level));
    }
}
