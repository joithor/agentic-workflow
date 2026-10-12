using System.ComponentModel;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>
/// The command palette (UI spec §8). Sections: <b>Actions</b>, every catalog row not in Sindri, ready first, then
/// pending (shown, never run), then cli-only (copied); <b>Go to</b>, the pages with their keys; <b>Sindri</b>, Rebuild
/// views, Stop Sindri (which goes to Health, where it lives: §4.8; its confirm is U4) and Health. Typing filters by a
/// fuzzy match on labels and ids; ↑/↓ move between entries that do something; Enter runs one and Esc closes. U1 has no
/// selected row, so Actions lists the whole catalog. It decides nothing about an action: the catalog says what runs.
/// </summary>
internal sealed class CommandPaletteViewModel : ObservableObject
{
    // The Sindri section's catalog rows, in its order.
    private static readonly string[] _sindri = ["RebuildViews", "StopProcess"];

    // §8's G-chords (and ⌘, for Settings), by page.
    private static readonly (PageId Page, string Key)[] _pages =
    [
        (PageId.Home, "G H"), (PageId.Queue, "G Q"), (PageId.Decisions, "G D"), (PageId.Services, "G S"), (PageId.Health, "G ."), (PageId.Settings, "⌘,"),
    ];

    private readonly CatalogViewModel _catalog;
    private readonly Action<PageId> _goTo;
    private readonly Action<string> _remember;
    private IReadOnlyList<PaletteSection> _all = [];

    /// <summary>The palette over the catalog.</summary>
    /// <param name="catalog">The catalog (read on connect).</param>
    /// <param name="goTo">Shows a page.</param>
    /// <param name="remember">Records an entry run (the paletteHistory preference).</param>
    internal CommandPaletteViewModel(CatalogViewModel catalog, Action<PageId> goTo, Action<string> remember)
    {
        (_catalog, _goTo, _remember) = (catalog, goTo, remember);
        _catalog.PropertyChanged += OnCatalogChanged;
    }

    /// <summary>The palette's accessible name.</summary>
    public string Title { get; } = Labels.Palette;

    /// <summary>The query field's placeholder.</summary>
    public string Placeholder { get; } = Labels.Search;

    /// <summary>The line shown when nothing matches.</summary>
    public string NoMatches { get; } = Labels.NoMatches;

    /// <summary>What Enter does with a ready command: the one place the palette's run policy is decided (U1 finding F2).</summary>
    internal PaletteRunPolicy RunPolicy { get; set; } = PaletteRunPolicy.StayOpen;

    /// <summary>
    /// True when the list should scroll to keep the selection in view (after ↑/↓); false after it opens or refilters,
    /// when the list starts at the top with its first section heading in view (U1 finding F1).
    /// </summary>
    public bool FollowsSelection { get; private set; }

    /// <summary>True while open.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The typed query; setting it filters the entries.</summary>
    public string Query
    {
        get;
        set
        {
            Set(ref field, value ?? string.Empty);
            Filter();
        }
    } = string.Empty;

    /// <summary>The sections with matching entries, in order (a section with none is left out).</summary>
    public IReadOnlyList<PaletteSection> Sections { get; private set => Set(ref field, value); } = [];

    /// <summary>Every matching entry, in order.</summary>
    public IReadOnlyList<PaletteEntry> Entries { get; private set => Set(ref field, value); } = [];

    /// <summary>True when nothing matches the query.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>The selected entry (Enter runs it), or null when nothing that runs matches.</summary>
    public PaletteEntry? Selected
    {
        get;
        private set
        {
            field?.IsSelected = false;
            Set(ref field, value);
            field?.IsSelected = true;
        }
    }

    /// <summary>Opens it with an empty query, the first entry that runs selected.</summary>
    internal void Open()
    {
        _all = Build();
        Query = string.Empty;
        IsOpen = true;
    }

    /// <summary>Closes it (Esc) and clears the query.</summary>
    internal void Close()
    {
        IsOpen = false;
        Query = string.Empty;
    }

    /// <summary>Moves the selection over the entries that run (↑ −1, ↓ +1), stopping at either end.</summary>
    /// <param name="by">How far.</param>
    internal void Move(int by)
    {
        var runs = Entries.Where(e => e.IsEnabled).ToList();
        if (runs.Count == 0)
        {
            return;
        }

        var at = Selected is { } selected ? runs.IndexOf(selected) : -1;
        Selected = runs[Math.Clamp(at + by, 0, runs.Count - 1)];
    }

    /// <summary>
    /// Runs an entry (Enter, or a click): a page is shown, Stop Sindri goes to Health, a ready row is sent once, and a
    /// cli-only row is copied ("Copied", the palette stays open). A pending entry does nothing. What ran is remembered.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">Cancels a send or a copy.</param>
    /// <returns>A task that completes when it ran.</returns>
    internal async Task Run(PaletteEntry? entry, CancellationToken cancellationToken)
    {
        if (entry is not { IsEnabled: true })
        {
            return;
        }

        _remember(entry.Id);
        switch (entry)
        {
            case { Page: { } page }:
                Close();
                _goTo(page);
                break;
            case { Control.IsCliOnly: true }:
                await entry.Control.Copy(cancellationToken).ConfigureAwait(true);
                break;
            case { Control.Action: "StopProcess" }:
                Close();
                _goTo(PageId.Health);
                break;
            default:
                Close();
                await entry.Control!.Run(cancellationToken).ConfigureAwait(true);   // why: an enabled entry without a page has a control
                break;
        }
    }

    private List<PaletteSection> Build()
    {
        var actions = _catalog.Controls.Where(c => !_sindri.Contains(c.Action, StringComparer.Ordinal)).OrderBy(c => c.Kind)
            .Select(c => PaletteEntry.For(c, Runs(c)));
        var sindri = _sindri.Select(_catalog.For).OfType<ControlViewModel>().Select(c => PaletteEntry.For(c, Runs(c)))
            .Append(PaletteEntry.For(PageId.Health, "G ."));
        return
        [
            new(Labels.PaletteActions, [.. actions]),
            new(Labels.PaletteGoTo, [.. _pages.Select(p => PaletteEntry.For(p.Page, p.Key))]),
            new(Labels.Sindri, [.. sindri]),
        ];
    }

    // Enter does something with a ready row that has a sender, with Stop Sindri (it goes to Health) and with a cli-only row it can copy.
    private static bool Runs(ControlViewModel control) =>
        control.CanRun || (control.IsReady && control.Action == "StopProcess") || (control.IsCliOnly && control.CanCopy);

    private void Filter()
    {
        var query = new string([.. Query.Where(c => !char.IsWhiteSpace(c)).Select(char.ToLowerInvariant)]);
        Sections = [.. _all.Select(s => s with { Entries = [.. s.Entries.Where(e => e.Matches(query))] }).Where(s => s.Entries.Count > 0)];
        Entries = [.. Sections.SelectMany(s => s.Entries)];
        OnChanged(nameof(IsEmpty));
        Selected = Entries.FirstOrDefault(e => e.IsEnabled);
    }

    // A catalog read while it is open (a reconnect) replaces its entries.
    private void OnCatalogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsOpen && e.PropertyName == nameof(CatalogViewModel.Controls))
        {
            _all = Build();
            Filter();
        }
    }
}
