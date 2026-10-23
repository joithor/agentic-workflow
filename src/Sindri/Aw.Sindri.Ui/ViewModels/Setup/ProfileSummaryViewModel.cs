using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// The approval's summary in plain words (setup-once spec §5.2): each repo with its path and size, then the services, the allowance, the outside systems and what is kept for the
/// person. It shows the groups as served, in that order, leaves out an empty one and shows nothing when the API sends no summary.
/// </summary>
internal sealed class ProfileSummaryViewModel : ObservableObject
{
    /// <summary>The groups to show, in order; empty when there is nothing to approve.</summary>
    public IReadOnlyList<ProfileSummaryGroupViewModel> Groups { get; private set; } = [];

    /// <summary>True when there is a summary to show.</summary>
    public bool HasSummary => Groups.Count > 0;

#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property of the view model
    /// <summary>The heading over the groups.</summary>
    public string Title => Labels.ProfileSummaryTitle;
#pragma warning restore CA1822, MA0041

    /// <summary>Shows a served summary, or nothing for null.</summary>
    /// <param name="summary">The summary the API served.</param>
    internal void Show(ProfileSummaryView? summary)
    {
        Groups = summary is null ? [] : Build(summary);
        OnChanged(nameof(Groups));
        OnChanged(nameof(HasSummary));
    }

    private static List<ProfileSummaryGroupViewModel> Build(ProfileSummaryView summary)
    {
        List<ProfileSummaryGroupViewModel> groups =
        [
            .. summary.Repos.Select(r => new ProfileSummaryGroupViewModel(r.Name, Labels.ProfileSummaryRepoLine(r.Path, r.Files, r.Megabytes), r.Sentences)),
            Group(Labels.ProfileSummaryServices, summary.Services),
            Group(Labels.ProfileSummaryAllowance, summary.Allowance),
            Group(Labels.ProfileSummaryConnections, summary.Connections),
            Group(Labels.ProfileSummaryHumanOnly, summary.HumanOnly),
        ];
        return [.. groups.Where(g => g.Lines.Count > 0 || g.HasDetail)];
    }

    private static ProfileSummaryGroupViewModel Group(string heading, IReadOnlyList<string> lines) => new(heading, string.Empty, lines);
}
