using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Home's Workflows strip in the shell (<see cref="ShellViewModel"/>, continued; U3' T17): J / K across the panels, Enter, and the inline answer.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Home's Workflows strip.</summary>
    public WorkflowsStripViewModel Strip { get; }

    // The strip gives rows back to Needs you first and marks its rows stale with the views; "… N more" opens Workflows.
    private void HookStrip()
    {
        Strip.WorkflowsRequested += (_, _) => GoTo(PageId.Workflows);
        NeedsYou.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(NeedsYouViewModel.Rows), StringComparison.Ordinal))
            {
                Strip.SetNeedsYouShown(NeedsYou.Rows.Count);
            }
        };
        Health.Banner.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ViewsBanner.IsStalled), StringComparison.Ordinal))
            {
                Strip.SetStale(Health.Banner.IsStalled);
            }
        };
    }

    // Enter opens the selected row's instance; A answers it: the park row for the same instance when there is one (the one
    // answer form is that row's sheet), else the instance itself. Never a protected row: its button is the lock.
    private bool StripKeyPressed(string key, bool inSheet)
    {
        if (inSheet || !IsHome || Strip.SelectedRow is not { } row)
        {
            return false;
        }

        switch (key)
        {
            case "Enter":
                Navigate(row.Link);
                return true;
            case "A" when row.CanAnswer:
                Strip.Deselect();
                Navigate(ParkRowFor(row) is { } park ? LinkOf(park.Id) : row.Link);
                return true;
            default:
                return false;
        }
    }

    // The Needs-you row for an instance's park (rule, source: the P4 needs-you goldens serve no park row yet, so this reads
    // the shape the plan names): a PARKED or ESCALATED row whose subject is the instance (its address or its id), else the
    // row of the same item. None: the caller opens the workflow link.
    private NeedsYouRowViewModel? ParkRowFor(StripRowViewModel row)
    {
        var parks = NeedsYou.Seen.Rows.Where(r => IsParkChip(r.Entry.Chip.Word)).ToList();
        return parks.Find(r => r.Entry.Subject is { } s && (string.Equals(s.Id, row.Address, StringComparison.Ordinal) || string.Equals(s.Id, row.InstanceId, StringComparison.Ordinal)))
            ?? parks.Find(r => row.ItemId is { } item && string.Equals(r.Entry.ItemId, item, StringComparison.Ordinal));
    }

    private static bool IsParkChip(string word) => string.Equals(word, "PARKED", StringComparison.Ordinal) || string.Equals(word, "ESCALATED", StringComparison.Ordinal);

    // J / K on Home walk Needs you, then the strip, then Running, then Queued (and back); true when the key was used.
    private bool MoveOnHome(int step)
    {
        var panels = NeedsYou.Rows.Count + Strip.Rows.Count;
        var at = PanelPosition();
        if (at >= 0)
        {
            WalkPanels(at, step, panels);
        }
        else if (panels > 0 && (!Queue.HasSelectionOn(QueueScope.Home) || (step < 0 && Queue.IsFirstOnHome)))
        {
            SelectPanelRow(step < 0 ? panels - 1 : 0);
        }
        else
        {
            LeavePanels(step);
        }

        return true;
    }

    // The selected row's place across Needs you and the strip; -1 when neither holds the selection.
    private int PanelPosition()
    {
        var at = IndexOfSelected();
        return at >= 0 ? at : Strip.SelectedRow is { } row ? NeedsYou.Rows.Count + Strip.Rows.ToList().IndexOf(row) : -1;
    }

    private void WalkPanels(int at, int step, int panels)
    {
        var needs = NeedsYou.Rows.Count;
        var to = at + step;
        if (to >= panels)
        {
            LeavePanels(step);
        }
        else if (at < needs && to >= 0 && to < needs)
        {
            _ = NeedsYou.Press(step > 0 ? NeedsYouKey.J : NeedsYouKey.K);
        }
        else
        {
            SelectPanelRow(Math.Max(to, 0));
        }
    }

    private void LeavePanels(int step)
    {
        NeedsYou.Deselect();
        Strip.Deselect();
        Queue.Move(step, QueueScope.Home);
    }

    private void SelectPanelRow(int index)
    {
        var needs = NeedsYou.Rows.Count;
        Queue.ClearSelection();
        if (index < needs)
        {
            Strip.Deselect();
            NeedsYou.Select(NeedsYou.Rows[index].Id);
        }
        else
        {
            NeedsYou.Deselect();
            _ = Strip.Select(Strip.Rows[index - needs].Address);
        }
    }
}
