using System.ComponentModel;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>
/// One palette entry (UI spec §8): a catalog row (its control, and its status: "ready", "arrives in P3" or
/// "Copy: sindri …", "Copied" for 1.5 s after a copy) or a page to go to, each with its key.
/// </summary>
internal sealed class PaletteEntry : ObservableObject
{
    private PaletteEntry(string id, string label, string key, bool enabled, ControlViewModel? control, PageId? page)
    {
        (Id, Label, Key, IsEnabled, Control, Page) = (id, label, key, enabled, control, page);
        control?.PropertyChanged += OnControlChanged;
    }

    /// <summary>Its id: the catalog action id, or "go:" and the page.</summary>
    public string Id { get; }

    /// <summary>Its label.</summary>
    public string Label { get; }

    /// <summary>Its key ("G Q", the catalog's shortcut), or empty.</summary>
    public string Key { get; }

    /// <summary>True when it shows a key.</summary>
    public bool HasKey => Key.Length > 0;

    /// <summary>A catalog row's status, or empty for a page.</summary>
    public string Status => Control switch
    {
        null => string.Empty,
        { IsReady: true } => Labels.Ready,
        { IsPending: true } => Labels.Arrives(Control.Stage),
        { CopyLabel: Labels.Copied } => Labels.Copied,
        _ => Labels.CopyCli(Control.Cli),
    };

    /// <summary>True while its command is being sent (the spinner; the label stays).</summary>
    public bool IsSending { get; }

    /// <summary>True after Sindri refused or failed its command: the code is its status and the detail shows under it.</summary>
    public bool IsFailed { get; }

    /// <summary>The refusal's detail as served, or empty.</summary>
    public string ErrorDetail { get; } = string.Empty;

    /// <summary>True when it has a status.</summary>
    public bool HasStatus => Control is not null;

    /// <summary>Its accessible name: its label and its status.</summary>
    public string Name => HasStatus ? Label + ", " + Status : Label;

    /// <summary>True for a catalog row.</summary>
    public bool IsAction => Control is not null;

    /// <summary>True when Enter does something (a pending row never does).</summary>
    public bool IsEnabled { get; }

    /// <summary>True while it is the palette's selection.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }

    /// <summary>A catalog row's control, or null for a page.</summary>
    public ControlViewModel? Control { get; }

    /// <summary>A page entry's page, or null.</summary>
    public PageId? Page { get; }

    /// <summary>A catalog row's entry: it runs when ready and wired, or for Stop Sindri (which goes to Health), and copies when cli-only.</summary>
    /// <param name="control">The row's control.</param>
    /// <param name="runs">True when Enter does something with it.</param>
    /// <returns>The entry.</returns>
    internal static PaletteEntry For(ControlViewModel control, bool runs) => new(control.Action, control.Label, control.Shortcut, runs, control, page: null);

    /// <summary>A page's entry.</summary>
    /// <param name="page">The page.</param>
    /// <param name="key">Its key.</param>
    /// <returns>The entry.</returns>
    internal static PaletteEntry For(PageId page, string key) => new("go:" + page, Labels.Page(page), key, enabled: true, control: null, page);

    /// <summary>True when every character of <paramref name="query"/> appears in order in its label or its id (§8's fuzzy match).</summary>
    /// <param name="query">The query, lower case, without spaces.</param>
    /// <returns>True when it matches.</returns>
    internal bool Matches(string query) => query.Length == 0 || Subsequence(query, Label) || Subsequence(query, Id);

    private static bool Subsequence(string query, string text)
    {
        var at = 0;
        foreach (var c in text)
        {
            if (at < query.Length && char.ToLowerInvariant(c) == query[at])
            {
                at++;
            }
        }

        return at == query.Length;
    }

    private void OnControlChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ControlViewModel.CopyLabel))
        {
            OnChanged(nameof(Status));
            OnChanged(nameof(Name));
        }
    }
}
