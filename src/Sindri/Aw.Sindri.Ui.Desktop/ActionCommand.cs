using System.Windows.Input;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>A tray menu item's command: always enabled, it runs one action.</summary>
/// <param name="run">The action.</param>
internal sealed class ActionCommand(Action run) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => run();
}
