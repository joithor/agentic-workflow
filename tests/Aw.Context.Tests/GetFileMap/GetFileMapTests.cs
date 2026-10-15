using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetFileMap;

public sealed class GetFileMapTests : IDisposable
{
    private static readonly ContextRepoName _repo = ContextRepoName.From(FixtureMap.Repo);

    private static readonly Dictionary<string, string> _tree = new(StringComparer.Ordinal)
    {
        ["src/tabs/TabManager.ts"] = "export const a = 1;",
        ["src/tabs/TabGroups.ts"] = "export const b = 1;",
        ["src/tabs/Pinned.ts"] = "export const c = 1;",
        ["src/layout/Grid.ts"] = "export const d = 1;",
        ["README.md"] = "# app",
        [".env"] = "TOKEN=hunter2",
    };

    private readonly MapWorld _world = new(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, _tree));

    public void Dispose() => _world.Dispose();

    private async Task ImportTabs() =>
        _ = await _world.ImportYaml(MapWorld.TabsMap);

    private async Task<Kernel.Result<FileMapView>> Ask(string path = "src/", bool unmapped = false, bool draft = false, ContextScope? scope = null, ContextRepoName? repo = null) =>
        await _world.Engine.FileMap(new FileMapQuery(repo ?? _repo, path, unmapped, draft), scope ?? MapWorld.Human, MapWorld.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_directory_lists_its_direct_children_with_a_status_each_and_totals()
    {
        await ImportTabs();

        var view = (await Ask()).Value;

        Assert.Equal(("v1", false, "src/"), (view.MapVersion, view.Draft, view.Path));
        Assert.Equal(["layout/", "tabs/"], view.Entries.Select(e => e.Name + "/"), StringComparer.Ordinal);
        Assert.All(view.Entries, e => Assert.Equal(FileEntryType.Dir, e.Type));
        Assert.Equal(new FileMapTotals(Files: 4, Mapped: 1, Suggested: 1, Unmapped: 2), view.Totals);
        var tabs = view.Entries.Single(e => e.Name == "tabs");
        Assert.Equal((3, FileMapStatus.Unmapped), (tabs.Files, tabs.Status));
        Assert.DoesNotContain(".env", view.Entries.Select(e => e.Name), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("./src")]
    [InlineData("/src/")]
    [InlineData("src//")]
    [InlineData("./src/./")]
    public async Task A_path_written_with_a_leading_dot_or_slash_or_a_doubled_one_lists_the_same_directory(string path)
    {
        await ImportTabs();

        var view = (await Ask(path)).Value;

        Assert.Equal("src/", view.Path);
        Assert.Equal(["layout/", "tabs/"], view.Entries.Select(e => e.Name + "/"), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/")]
    [InlineData(".")]
    [InlineData("./")]
    public async Task The_root_is_written_as_nothing_a_slash_or_a_dot(string path)
    {
        await ImportTabs();

        var view = (await Ask(path)).Value;

        Assert.Equal(string.Empty, view.Path);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_suggested_file_carries_the_id_of_the_link_to_accept()
    {
        await ImportTabs();

        var view = (await Ask("src/tabs/")).Value;

        var suggestion = Assert.Single(view.Suggested);
        Assert.Equal(("src/tabs/TabGroups.ts", "feature:tabs.tab-groups", 0.74), (suggestion.File, suggestion.Feature.Value, suggestion.Confidence));
        Assert.Equal(view.Entries.Single(e => e.Name == "TabGroups.ts").Features[0].LinkId, suggestion.LinkId);
        Assert.Equal(FileMapStatus.Suggested, view.Entries.Single(e => e.Name == "TabGroups.ts").Status);
        Assert.Equal(FileMapStatus.Mapped, view.Entries.Single(e => e.Name == "TabManager.ts").Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_file_is_neither_listed_nor_suggested_before_any_rebuild()
    {
        await ImportTabs();
        var denying = new ContextScope(Settings: null, [MapWorld.Repos[0] with { DenyPaths = [.. PathDenylist.Defaults, "src/tabs/TabGroups.ts"] }], MapWorld.Human.Caller);

        var view = (await Ask("src/tabs/", scope: denying)).Value;

        Assert.DoesNotContain(view.Entries, e => string.Equals(e.Name, "TabGroups.ts", StringComparison.Ordinal));
        Assert.Empty(view.Suggested);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_view_names_the_commit_the_tree_was_listed_at_by_its_first_seven_characters()
    {
        await ImportTabs();

        var view = (await Ask()).Value;

        Assert.Equal((MapWorld.Head[..7], 1, "aw-split-panes"), (view.Sha, view.SchemaVersion, view.Repo.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Asking_for_the_unmapped_keeps_the_suggested_and_the_unmapped()
    {
        await ImportTabs();

        var view = (await Ask("src/tabs/", unmapped: true)).Value;

        Assert.Equal(["Pinned.ts", "TabGroups.ts"], view.Entries.Select(e => e.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_root_lists_the_top_level_files_and_directories()
    {
        await ImportTabs();

        var view = (await Ask(path: string.Empty)).Value;

        Assert.Equal(["src", "README.md"], view.Entries.Select(e => e.Name), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("nope")]
    [InlineData("other")]
    public async Task A_repo_outside_the_profile_or_the_sessions_scope_is_SND_CONTEXT_045_with_one_answer(string repo)
    {
        await ImportTabs();

        var human = await Ask(scope: MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), repo: ContextRepoName.From(repo));
        var session = await Ask(scope: MapWorld.Session(FixtureMap.Repo), repo: ContextRepoName.From(repo));

        Assert.Equal((ContextErrors.RepoOutsideScope.Code, ContextErrors.RepoOutsideScope.Code), (human.Error.Code, session.Error.Code));
        Assert.Equal(human.Error.Message, session.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_path_with_no_file_is_003_no_draft_is_044_no_map_is_030_and_sessions_off_is_020()
    {
        var noMap = await Ask();
        await ImportTabs();

        var missing = await Ask("src/nowhere/");
        var noDraft = await Ask(draft: true);
        var off = await Ask(scope: MapWorld.Session(FixtureMap.Repo, enabled: false));

        Assert.Equal(
            (ContextErrors.NoMap.Code, ContextErrors.UnknownAnchor.Code, ContextErrors.NoDraftMap.Code, ContextErrors.SessionsDisabled.Code),
            (noMap.Error.Code, missing.Error.Code, noDraft.Error.Code, off.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_git_cant_read_is_SND_CONTEXT_048()
    {
        await ImportTabs();
        using var broken = new MapWorld(new FakeRepoReader(MapWorld.Repos[1], MapWorld.Head, _tree));
        _ = await broken.ImportYaml(MapWorld.TabsMap);

        var view = await broken.Engine.FileMap(new FileMapQuery(_repo, "src/", UnmappedOnly: false, Draft: false), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.ExtractionUnreadable.Code, view.Error.Code);
    }
}
