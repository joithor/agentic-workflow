using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The control plane's health (<see cref="HealthViewModel"/>, continued): the unreadable and mismatch states and how a state and its reason change together.</summary>
internal sealed partial class HealthViewModel
{
    private Uri? Unreadable(Error error)
    {
        lock (_gate)
        {
            _decisions++;
            Become(ShellState.Unreadable, $"{error.Code}: {error.Message}");
            Word();
        }

        return null;
    }

    private void Mismatch(int version)
    {
        lock (_gate)
        {
            _decisions++;
            Detail.Mismatch(version);
            HealthLine = Labels.VersionMismatch;
            Become(ShellState.VersionMismatch, Labels.Versions(version, SindriApiClient.AppApiVersion));
            Word();
        }
    }

    // A state and its reason are one change (review run 3, julik-frontend-races-reviewer-0): both are set before either
    // is raised, State first, so whoever reacts to State (the shell's live regions) already sees the new reason, and the
    // old state's region never speaks it. Raised under the gate, like every other change here.
    private void Become(ShellState state, string reason)
    {
        var (stateChanged, reasonChanged) = (State != state, !string.Equals(Reason, reason, StringComparison.Ordinal));
        (State, Reason) = (state, reason);
        if (stateChanged)
        {
            OnChanged(nameof(State));
        }

        if (reasonChanged)
        {
            OnChanged(nameof(Reason));
        }
    }
}
