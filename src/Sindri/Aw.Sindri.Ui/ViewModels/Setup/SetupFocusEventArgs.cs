namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>A form asks for focus in one of its controls.</summary>
internal sealed class SetupFocusEventArgs : EventArgs
{
    internal SetupFocusEventArgs(SetupFocus target)
    {
        Target = target;
    }

    /// <summary>The control to focus.</summary>
    internal SetupFocus Target { get; }
}
