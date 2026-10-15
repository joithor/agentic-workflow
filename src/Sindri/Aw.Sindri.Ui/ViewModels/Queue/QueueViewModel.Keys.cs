using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>
/// §8's guards and filter (<see cref="QueueViewModel"/>, continued): while the pointer is over a panel new rows are held
/// behind an "N new" pill (spoken) instead of shifting the rows under it, while removals and updates show at once; for 500 ms after a refresh moved the selection on its
/// own, single keys are ignored; `/` filters the Queue page by id, title and repo; `Y` copies the selected row's CLI.
/// </summary>
internal sealed partial class QueueViewModel
{
    /// <summary>§8: how long single keys are ignored after focus moved on its own.</summary>
    internal static readonly TimeSpan KeyGuard = TimeSpan.FromMilliseconds(500);

    private bool _holding;
    private QueueView? _held;
    private DateTimeOffset? _movedAt;

    /// <summary>True while a held refresh brought rows the page doesn't show yet.</summary>
    public bool HasHeld => HeldCount > 0;

    /// <summary>"N new": the pill over the panel while rows are held.</summary>
    public string HeldText => Labels.NewRows(HeldCount);

    /// <summary>True for <see cref="KeyGuard"/> after a refresh moved the selection because its row cleared.</summary>
    public bool IsGuarded => _movedAt is { } at && clock.GetUtcNow() - at < KeyGuard;

    /// <summary>True while the Queue page's filter field is open (`/`).</summary>
    public bool IsFiltering { get; private set => Set(ref field, value); }

    /// <summary>The filter's text: rows whose id, title or repo contain it (any case) stay.</summary>
    public string FilterText
    {
        get;
        set
        {
            Set(ref field, value ?? string.Empty);
            Show();
        }
    } = string.Empty;

    private int HeldCount { get; set { field = value; OnChanged(nameof(HasHeld)); OnChanged(nameof(HeldText)); } }

    /// <summary>The pointer entered (true) or left (false) a panel; leaving shows what was held.</summary>
    /// <param name="holding">True while the pointer is over a panel.</param>
    internal void Hold(bool holding)
    {
        _holding = holding;
        if (!holding)
        {
            ShowHeld();
        }
    }

    /// <summary>The pill: shows the held rows now.</summary>
    internal void ShowHeld()
    {
        if (_held is { } view)
        {
            (_held, HeldCount) = (null, 0);
            Apply(view);
        }
    }

    /// <summary>`/`: opens the filter field.</summary>
    internal void OpenFilter() => IsFiltering = true;

    /// <summary>Esc on the filter: clears and closes it.</summary>
    internal void ClearFilter()
    {
        FilterText = string.Empty;
        IsFiltering = false;
    }

    /// <summary>`Y`: the selected row's primary action, when it has a CLI to copy.</summary>
    /// <returns>The control, or null.</returns>
    internal ControlViewModel? SelectedCopy() =>
        Groups.SelectMany(g => g.Rows).FirstOrDefault(r => r.Id == SelectedId)?.PrimaryAction is { CanCopy: true } control ? control : null;

    // While holding, only rows the page doesn't show yet wait behind the pill (they would shift the rows under the
    // pointer); removals and in-place updates show at once, so nothing stale sits under it. Returns what to show now.
    private QueueView Hold(QueueView view)
    {
        if (!_holding || _view is not { } shown)
        {
            (_held, HeldCount) = (null, 0);
            return view;
        }

        var ids = shown.Groups.SelectMany(g => g.Items).Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var added = view.Groups.SelectMany(g => g.Items).Select(i => i.Id).Where(id => !ids.Contains(id)).ToHashSet(StringComparer.Ordinal);
        if (added.Count == 0)
        {
            (_held, HeldCount) = (null, 0);
            return view;
        }

        _held = view;
        if (HeldCount != added.Count)
        {
            HeldCount = added.Count;
            Say(HeldText);
        }

        return view with { Groups = [.. view.Groups.Select(g => g with { Items = [.. g.Items.Where(i => !added.Contains(i.Id))] })] };
    }

    private bool Matches(ItemRowViewModel row) =>
        FilterText.Length == 0 || row.Id.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
        || row.Title.Contains(FilterText, StringComparison.OrdinalIgnoreCase) || row.Repo.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
}
