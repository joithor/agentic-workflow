namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame asks the view to put focus on one of its parts.</summary>
/// <param name="target">Which part.</param>
/// <param name="key">The fact's key when the part is a fact's row; null otherwise.</param>
internal sealed class FrameFocusEventArgs(FrameFocus target, string? key) : EventArgs
{
    /// <summary>Which part.</summary>
    public FrameFocus Target { get; } = target;

    /// <summary>The fact's key, or null.</summary>
    public string? Key { get; } = key;
}
