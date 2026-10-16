using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The motion setting of a shell built without one: the OS asks for nothing.</summary>
internal sealed class StillMotion : IMotionPreference
{
    /// <inheritdoc />
    public bool ReduceMotion => false;

    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}
