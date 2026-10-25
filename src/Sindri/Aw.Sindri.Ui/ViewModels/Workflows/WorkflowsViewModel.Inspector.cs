namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The Workflows page's run drawer (<see cref="WorkflowsViewModel"/>, continued; F3 workflows spec §2, §5). A click or the first Enter on a run opens it;
/// Enter again on the open run goes to the run; J / K and the drawer's Previous and Next move the selection and the drawer follows; Esc closes it.
/// </summary>
internal sealed partial class WorkflowsViewModel
{
    /// <summary>The run drawer's view model.</summary>
    public RunInspectorViewModel Inspector => field ??= CreateInspector();

    /// <summary>True while the drawer shows.</summary>
    public bool IsInspectorOpen { get; private set => Set(ref field, value); }

    /// <summary>The read of the drawer's run in flight or last finished; completed before the first.</summary>
    internal Task InspectorRead { get; private set; } = Task.CompletedTask;

    private RunInspectorViewModel CreateInspector()
    {
        var inspector = new RunInspectorViewModel(api, clipboard, clock);
        inspector.OpenRequested += (_, e) => OpenRequested?.Invoke(this, e);
        inspector.CloseRequested += (_, _) => CloseInspector();
        inspector.StepRequested += (_, e) => _ = Move(e.Step);
        return inspector;
    }

    /// <summary>Selects a run and opens the drawer on it (a click).</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>True when a run of that address is shown.</returns>
    internal bool OpenInspector(string address)
    {
        if (!Select(address) || Shown.FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal)) is not { } row)
        {
            return false;
        }

        IsInspectorOpen = true;
        Load(row);
        return true;
    }

    /// <summary>Closes the drawer (Esc, its close button).</summary>
    /// <returns>True when it was open.</returns>
    internal bool CloseInspector()
    {
        if (!IsInspectorOpen)
        {
            return false;
        }

        IsInspectorOpen = false;
        return true;
    }

    // Enter: the first one opens the drawer on the run, the second (the drawer already on it) opens the run.
    private void EnterOn(InstanceRowViewModel row)
    {
        if (IsInspectorOpen && string.Equals(Inspector.Address, row.Address, StringComparison.Ordinal))
        {
            OpenRequested?.Invoke(this, new OpenRequestedEventArgs(row.Link));
        }
        else
        {
            _ = OpenInspector(row.Address);
        }
    }

    // After the selection moved or a read changed the rows: the open drawer follows the selected run, shows its latest row, and closes when nothing is selected.
    private void FollowInspector()
    {
        if (!IsInspectorOpen)
        {
            return;
        }

        if (SelectedAddress is not { } address || Shown.FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal)) is not { } row)
        {
            IsInspectorOpen = false;
            return;
        }

        if (Inspector.Row is not { } shown || !string.Equals(shown.Address, address, StringComparison.Ordinal) || !string.Equals(shown.Signature, row.Signature, StringComparison.Ordinal))
        {
            Load(row);
        }

        var walk = Walk;
        Inspector.SetPosition(walk.FindIndex(r => string.Equals(r.Address, address, StringComparison.Ordinal)) + 1, walk.Count);
    }

    private void Load(InstanceRowViewModel row)
    {
        InspectorRead = Inspector.Show(Url, row, CancellationToken.None);
        var walk = Walk;
        Inspector.SetPosition(walk.FindIndex(r => string.Equals(r.Address, row.Address, StringComparison.Ordinal)) + 1, walk.Count);
    }
}
