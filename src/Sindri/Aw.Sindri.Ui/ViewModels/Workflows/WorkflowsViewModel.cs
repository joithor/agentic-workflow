using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

#pragma warning disable MA0182 // a red stub until the shell holds it
/// <summary>The Workflows page (screens §4.2): the open root instances grouped by recipe, as the API serves them.</summary>
internal sealed class WorkflowsViewModel : ObservableObject
{
    internal WorkflowsViewModel(ISindriApi api)
    {
        _ = api;
    }

    /// <summary>The groups, in the order the API first served their recipes.</summary>
    public ObservableCollection<RecipeGroupViewModel> Groups { get; } = [];

    /// <summary>"8 open · 2 need you · 1 with you".</summary>
    public string Head { get; } = string.Empty;

    /// <summary>The nav tab's count.</summary>
    public string NavCount { get; } = string.Empty;

    /// <summary>True when finished instances exist.</summary>
    public bool HasDone { get; }

    /// <summary>"4 done today".</summary>
    public string DoneText { get; } = string.Empty;

    /// <summary>True while the finished rows show.</summary>
    public bool IsDoneExpanded { get; }

    /// <summary>The finished root rows, once the line is expanded.</summary>
    public ObservableCollection<InstanceRowViewModel> DoneRows { get; } = [];

    /// <summary>True when the list is empty and served.</summary>
    public bool IsEmpty { get; }

    /// <summary>The served empty sentence.</summary>
    public string EmptyText { get; } = string.Empty;

    /// <summary>True while the list answers in a shape this build can't read.</summary>
    public bool IsUnreadable { get; }

    /// <summary>What the page says then.</summary>
    public string UnreadableText { get; } = string.Empty;

    /// <summary>Re-reads the list.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Narrows the list to one recipe, or to all.</summary>
    /// <param name="recipe">A recipe id, or null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task FilterRecipe(string? recipe, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Narrows the list to one subject, or to all.</summary>
    /// <param name="subject">A subject key or label, or null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task FilterSubject(string? subject, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Expands or collapses the finished line.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the line is shown.</returns>
    internal Task ToggleDone(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Marks the rows stale while the views are stalled or rebuilding.</summary>
    /// <param name="stale">True while stale.</param>
    internal void SetStale(bool stale) => throw new NotSupportedException();
}
