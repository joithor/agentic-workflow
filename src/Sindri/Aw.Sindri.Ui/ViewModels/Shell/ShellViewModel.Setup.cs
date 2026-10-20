using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Approvals;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Setup card and page in the shell (<see cref="ShellViewModel"/>, continued; setup-once spec §5.7): the list is read with every event, whatever page is shown, because the card
/// is on Home and a step can reopen; the card's link, G U and the palette open the page; a step's button opens approve-all or the profile's approval row; and Home hides a setup row
/// for a step the card draws, so the card and Needs you never show one approval twice.
/// </summary>
internal sealed partial class ShellViewModel
{
    private const string SetupRowKind = "setup";
    private const string ProfileDriftRow = "profile:drift";

    /// <summary>The setup list behind Home's card and the Setup page.</summary>
    public SetupViewModel Setup { get; }

    /// <summary>True on Setup.</summary>
    public bool IsSetup => CurrentPage == PageId.Setup;

    // The app does approve-all itself once the catalog says it may, and opens the profile's approval row when Needs you has it; every other action is a command to copy until its form exists (U3).
    private bool OpensInApp(string action) => action switch
    {
        ApproveAllViewModel.Action => ApproveAll.IsReady,
        "ApproveProfile" => NeedsYou.Seen.Rows.Any(r => string.Equals(r.Id, ProfileDriftRow, StringComparison.Ordinal)),
        _ => false,
    };

    private void InitSetup()
    {
        Health.Follow(Setup.Refresh);
        Setup.PageRequested += (_, _) => GoTo(PageId.Setup);
        Setup.ActionRequested += (_, e) => OpenSetupAction(e.Action);
        Setup.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(SetupViewModel.DrawnIds), StringComparison.Ordinal))
            {
                UpdateNeedsYou();
            }
        };
        Health.Catalog.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(CatalogViewModel.Controls), StringComparison.Ordinal))
            {
                Setup.Rederive();
            }
        };
        NeedsYou.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(NeedsYouViewModel.Rows), StringComparison.Ordinal))
            {
                Setup.Rederive();
            }
        };
    }

    private void OpenSetupAction(string action)
    {
        if (string.Equals(action, ApproveAllViewModel.Action, StringComparison.Ordinal))
        {
            OpenApproveAll();
        }
        else if (string.Equals(action, "ApproveProfile", StringComparison.Ordinal))
        {
            Navigate(LinkOf(ProfileDriftRow));
        }
    }

    // Home's list: the decisions filter, and no setup row for a step the card is drawing (the Needs you list and the tray keep every row; the card links the profile row rather than copying it).
    private bool ShownOnHome(NeedsYouEntry row) =>
        (HomeFilter != HomeFilter.Decisions || row.DecisionId is not null)
        && !(string.Equals(row.Kind, SetupRowKind, StringComparison.Ordinal) && row.Subject is { } subject && Setup.DrawnIds.Contains(subject.Id));
}
