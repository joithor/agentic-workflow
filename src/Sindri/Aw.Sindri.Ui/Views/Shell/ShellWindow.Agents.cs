using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The Agents page and the window (<see cref="ShellWindow"/>, continued; U11a): a minimised window does not poll for sessions.</summary>
internal sealed partial class ShellWindow
{
    private IDisposable? _agentsWindowState;

    private void ListenAgents(ShellViewModel shell, bool on)
    {
        _agentsWindowState?.Dispose();
        _agentsWindowState = on ? this.GetObservable(WindowStateProperty).Subscribe(new WindowStateObserver(shell)) : null;
    }

    private sealed class WindowStateObserver(ShellViewModel shell) : IObserver<WindowState>
    {
        public void OnNext(WindowState value) => shell.SetWindowMinimised(value == WindowState.Minimized);

        public void OnError(Exception error)
        {
            // The window state never faults.
        }

        public void OnCompleted()
        {
            // Nothing to finish: the subscription ends with the window.
        }
    }
}
