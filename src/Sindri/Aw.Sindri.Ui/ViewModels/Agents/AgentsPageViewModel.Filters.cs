using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Agents page's filters (<see cref="AgentsPageViewModel"/>, continued; Q2): provider, state and repo, applied in the app over the one
/// list that was read. The head's counts are the API's and never change with a filter.
/// </summary>
internal sealed partial class AgentsPageViewModel
{
    private static readonly string[] _states = ["running", "asking", "idle", "unknown"];

    /// <summary>The provider choices: all, then each provider the list has shown.</summary>
    public ObservableCollection<string> ProviderChoices { get; } = [Labels.AllProviders];

    /// <summary>The state choices: all, then the four states.</summary>
    public ObservableCollection<string> StateChoices { get; } = [Labels.AllStates, .. _states];

    /// <summary>The repo choices: all, then each repo the list has shown.</summary>
    public ObservableCollection<string> RepoChoices { get; } = [Labels.AllRepos];

    /// <summary>The provider label the list is narrowed to; null for all.</summary>
    public string? ProviderFilter { get; private set => Set(ref field, value); }

    /// <summary>The state word the list is narrowed to; null for all.</summary>
    public string? StateFilter { get; private set => Set(ref field, value); }

    /// <summary>The repo the list is narrowed to; null for all.</summary>
    public string? RepoFilter { get; private set => Set(ref field, value); }

    /// <summary>The provider filter's accessible name: its shown choice first.</summary>
    public string ProviderFilterName => Labels.FilterName(ProviderFilter ?? Labels.AllProviders, "provider");

    /// <summary>The state filter's accessible name: its shown choice first.</summary>
    public string StateFilterName => Labels.FilterName(StateFilter ?? Labels.AllStates, "status");

    /// <summary>The repo filter's accessible name: its shown choice first.</summary>
    public string RepoFilterName => Labels.FilterName(RepoFilter ?? Labels.AllRepos, "repo");

    /// <summary>Narrows the list to one provider (a choice), or to all.</summary>
    /// <param name="choice">A provider label, or the "all" choice, or null.</param>
    internal void FilterProvider(string? choice)
    {
        ProviderFilter = Chosen(choice, Labels.AllProviders);
        OnChanged(nameof(ProviderFilterName));
        Redraw();
    }

    /// <summary>Narrows the list to one state, or to all.</summary>
    /// <param name="choice">A state word, or the "all" choice, or null.</param>
    internal void FilterState(string? choice)
    {
        StateFilter = Chosen(choice, Labels.AllStates);
        OnChanged(nameof(StateFilterName));
        Redraw();
    }

    /// <summary>Narrows the list to one repo, or to all.</summary>
    /// <param name="choice">A repo, or the "all" choice, or null.</param>
    internal void FilterRepo(string? choice)
    {
        RepoFilter = Chosen(choice, Labels.AllRepos);
        OnChanged(nameof(RepoFilterName));
        Redraw();
    }

    private static string? Chosen(string? choice, string all) => string.IsNullOrEmpty(choice) || string.Equals(choice, all, StringComparison.Ordinal) ? null : choice;

    private bool Matches(SessionRowView row) =>
        (ProviderFilter is null || string.Equals(row.ProviderLabel, ProviderFilter, StringComparison.Ordinal))
        && (StateFilter is null || string.Equals(row.State, StateFilter, StringComparison.Ordinal))
        && (RepoFilter is null || string.Equals(row.Repo, RepoFilter, StringComparison.Ordinal));

    private void Redraw()
    {
        if (_list is { } list)
        {
            Apply(WalkKeys(), list);
        }
    }

    // The choices follow what the list has shown; a filter that is set stays a choice even when nothing matches it any more.
    private void ReplaceChoices(SessionList list)
    {
        var all = list.Sessions.Concat(list.RecentlyEnded.Select(e => e.Session)).ToList();
        Replace(ProviderChoices, Labels.AllProviders, [.. all.Select(s => s.ProviderLabel), .. Extra(ProviderFilter)]);
        Replace(RepoChoices, Labels.AllRepos, [.. all.Select(s => s.Repo).OfType<string>(), .. Extra(RepoFilter)]);
        Replace(StateChoices, Labels.AllStates, [.. _states, .. all.Select(s => s.State), .. Extra(StateFilter)]);
    }

    private static string[] Extra(string? value) => value is null ? [] : [value];

    private static void Replace(ObservableCollection<string> choices, string all, IEnumerable<string> values)
    {
        var target = new List<string> { all };
        target.AddRange(values.Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        if (choices.SequenceEqual(target, StringComparer.Ordinal))
        {
            return;   // the control keeps its selection unless the choices themselves changed
        }

        // Edit in place: clearing the collection would drop the combo box's selection (and so clear the filter it stands for).
        foreach (var gone in choices.Where(c => !target.Contains(c, StringComparer.Ordinal)).ToList())
        {
            _ = choices.Remove(gone);
        }

        for (var i = 0; i < target.Count; i++)
        {
            if (i < choices.Count && string.Equals(choices[i], target[i], StringComparison.Ordinal))
            {
                continue;
            }

            var at = choices.IndexOf(target[i]);
            if (at >= 0)
            {
                choices.Move(at, i);
            }
            else
            {
                choices.Insert(i, target[i]);
            }
        }
    }
}
