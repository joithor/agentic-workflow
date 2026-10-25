namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>A step's action the app opens itself. The shell decides what opening it means.</summary>
internal sealed class SetupActionEventArgs : EventArgs
{
    internal SetupActionEventArgs(string action)
    {
        Action = action;
    }

    /// <summary>The catalog action id.</summary>
    internal string Action { get; }
}
