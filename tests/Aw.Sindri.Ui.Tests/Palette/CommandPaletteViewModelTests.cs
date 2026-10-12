using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Palette;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Palette;

/// <summary>
/// The command palette (UI spec §8): Actions (every catalog row not in Sindri, grouped by status), Go to, Sindri
/// (Rebuild views, Stop Sindri, Health); fuzzy matching on labels and ids; ↑/↓, Enter, Esc; the history of entries run.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class CommandPaletteViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSindriApi _server = new();
    private readonly List<PageId> _pages = [];
    private readonly List<string> _history = [];
    private readonly CommandList _catalog = AwJson.Deserialize<CommandList>(Encoding.UTF8.GetBytes(GoldenFiles.Api("commands", "catalog")), SindriApiClient.Json).Value;
    private readonly CommandPaletteViewModel _palette;

    public CommandPaletteViewModelTests()
    {
        var catalog = new CatalogViewModel(_clipboard, _clock);
        catalog.Show(_api, _server, _catalog);
        _palette = new CommandPaletteViewModel(catalog, _pages.Add, _history.Add);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private PaletteEntry Entry(string id) => _palette.Entries.Single(e => e.Id == id);

    // The U1 proof: every catalog row appears once, each with its status.
    [Fact]
    public void Open_lists_every_catalog_row_once_with_its_status()
    {
        _palette.Open();

        Assert.True(_palette.IsOpen);
        Assert.Equal(["Actions", "Go to", "Sindri"], _palette.Sections.Select(s => s.Heading), StringComparer.Ordinal);
        var actions = _palette.Entries.Where(e => e.IsAction).Select(e => e.Id).ToList();
        Assert.Equal(_catalog.Rows.Select(r => r.Action).Order(StringComparer.Ordinal), actions.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(("ready", "arrives in P6", "Copy: sindri doctor"), (Entry("RebuildViews").Status, Entry("CancelJob").Status, Entry("RunDoctor").Status));
        Assert.Equal("Cancel job, arrives in P6", Entry("CancelJob").Name);
    }

    // Q3: with no selected row, Actions lists the catalog grouped by status: ready, then pending, then cli-only.
    [Fact]
    public void Actions_are_grouped_by_status_and_rebuild_and_stop_sit_in_sindri()
    {
        _palette.Open();

        var actions = _palette.Sections[0].Entries;
        var kinds = actions.Select(e => e.Control!.Kind).ToList();
        Assert.Equal(kinds.Order(), kinds);
        Assert.DoesNotContain("RebuildViews", actions.Select(e => e.Id), StringComparer.Ordinal);
        Assert.DoesNotContain("StopProcess", actions.Select(e => e.Id), StringComparer.Ordinal);
        Assert.Equal(["Rebuild views", "Stop Sindri", "Health"], _palette.Sections[2].Entries.Select(e => e.Label), StringComparer.Ordinal);
    }

    [Fact]
    public void Go_to_lists_the_pages_with_their_keys()
    {
        _palette.Open();

        Assert.Equal(
            ["Home G H", "Queue G Q", "Decisions G D", "Services G S", "Health G .", "Settings ⌘,"],
            _palette.Sections[1].Entries.Select(e => e.Label + " " + e.Key),
            StringComparer.Ordinal);
    }

    // §8: fuzzy matching on labels and ids.
    [Theory]
    [InlineData("rebuild", new[] { "RebuildViews" })]
    [InlineData("REBVIEWS", new[] { "RebuildViews" })]
    [InlineData("probelaunch", new[] { "ProbeLaunchTargets" })]
    [InlineData("stop sin", new[] { "StopProcess" })]
    public void Typing_filters_by_label_and_id(string query, string[] ids)
    {
        _palette.Open();

        _palette.Query = query;

        Assert.Equal(ids, _palette.Entries.Select(e => e.Id), StringComparer.Ordinal);
        Assert.All(_palette.Sections, s => Assert.NotEmpty(s.Entries));
    }

    [Fact]
    public void Nothing_matching_leaves_no_sections_and_no_selection()
    {
        _palette.Open();

        _palette.Query = "zzzz";

        Assert.Empty(_palette.Sections);
        Assert.Null(_palette.Selected);
    }

    // ↑/↓ move between entries that run; a pending entry is shown but never selected.
    [Fact]
    public void Arrows_move_the_selection_over_entries_that_run()
    {
        _palette.Open();
        Assert.Equal("ApproveProfile", _palette.Selected?.Id);   // no ready row is in Actions: the first that runs is a copy block

        _palette.Move(1);
        Assert.Equal("RunDoctor", _palette.Selected?.Id);
        _palette.Move(-1);
        _palette.Move(-1);
        Assert.Equal("ApproveProfile", _palette.Selected?.Id);
        Assert.True(_palette.Selected!.IsSelected);
        Assert.All(_palette.Entries.Where(e => e.Control?.IsPending == true), e => Assert.False(e.IsEnabled));
    }

    // Q4: Stop Sindri goes to Health, where it lives (§4.8); it is never sent from the palette.
    [Fact]
    public async Task Stop_sindri_goes_to_health_and_closes()
    {
        _palette.Open();

        await _palette.Run(Entry("StopProcess"), Token);

        Assert.Equal([PageId.Health], _pages);
        Assert.False(_palette.IsOpen);
        Assert.Equal(["StopProcess"], _history, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Rebuild_views_runs_once_and_closes()
    {
        _palette.Open();

        await _palette.Run(Entry("RebuildViews"), Token);

        Assert.Equal(1, _server.RebuildCalls);
        Assert.False(_palette.IsOpen);
        Assert.Equal(["RebuildViews"], _history, StringComparer.Ordinal);
    }

    // A cli-only entry copies its CLI and says "Copied" for 1.5 s; the palette stays open.
    [Fact]
    public async Task A_cli_only_entry_copies_its_cli()
    {
        _palette.Open();
        _palette.Query = "approve";

        await _palette.Run(_palette.Selected, Token);

        Assert.Equal("sindri profile approve [<hash>]", _clipboard.Text);
        Assert.Equal(("Copied", true), (Entry("ApproveProfile").Status, _palette.IsOpen));
        _clock.Advance(ControlViewModel.CopiedFor);
        Assert.Equal("Copy: sindri profile approve [<hash>]", Entry("ApproveProfile").Status);
    }

    [Fact]
    public async Task A_pending_entry_does_nothing()
    {
        _palette.Open();

        await _palette.Run(Entry("CancelJob"), Token);

        Assert.Equal((true, 0, null), (_palette.IsOpen, _history.Count, _clipboard.Text));
    }

    [Fact]
    public async Task A_go_to_entry_shows_its_page()
    {
        _palette.Open();
        _palette.Query = "queue";

        await _palette.Run(_palette.Selected, Token);

        Assert.Equal([PageId.Queue], _pages);
        Assert.Equal(["go:Queue"], _history, StringComparer.Ordinal);
        Assert.False(_palette.IsOpen);
    }

    [Fact]
    public void Close_clears_the_query()
    {
        _palette.Open();
        _palette.Query = "rebuild";

        _palette.Close();
        _palette.Open();

        Assert.Equal((true, string.Empty), (_palette.IsOpen, _palette.Query));
        Assert.Equal(3, _palette.Sections.Count);
    }
}
