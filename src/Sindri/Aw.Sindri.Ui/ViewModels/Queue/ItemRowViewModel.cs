using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>
/// One item row (UI spec §4.5): id, title, repo, the state as a word, step, since, and the row's primary action from the
/// catalog. Every field is the API's; the row decides nothing.
/// </summary>
internal sealed class ItemRowViewModel : ObservableObject
{
    internal ItemRowViewModel(ItemSummaryView item, string group, ControlViewModel? primaryAction, DateTimeOffset now)
    {
        Id = item.Id;
        Group = group;
        Title = item.Title;
        Repo = item.Repo ?? string.Empty;
        StateWord = Labels.ItemState(item.State);
        Step = item.Step ?? string.Empty;
        Age = Labels.Age(now - item.Since);
        Name = Id + " " + Title + ", " + StateWord;
        PrimaryAction = primaryAction;
    }

    /// <summary>The item's id ('ITM-…').</summary>
    public string Id { get; }

    /// <summary>The API group the row came in.</summary>
    public string Group { get; }

    /// <summary>The title as served (plain text).</summary>
    public string Title { get; }

    /// <summary>The repo, or empty.</summary>
    public string Repo { get; }

    /// <summary>The item state as a word.</summary>
    public string StateWord { get; }

    /// <summary>The engine step, or empty before S2.</summary>
    public string Step { get; }

    /// <summary>True when the API names a step.</summary>
    public bool HasStep => Step.Length > 0;

    /// <summary>How long since the item entered its state ("12m").</summary>
    public string Age { get; }

    /// <summary>The row's accessible name: its id, title and state.</summary>
    public string Name { get; }

    /// <summary>The API's primaryAction as the catalog's control (pending rows drawn disabled), or null.</summary>
    public ControlViewModel? PrimaryAction { get; }

    /// <summary>True when the row has a primary action to show.</summary>
    public bool HasPrimaryAction => PrimaryAction is not null;

    /// <summary>True while the row is the selected one (J/K, a click).</summary>
    public bool IsSelected { get; set => Set(ref field, value); }
}
