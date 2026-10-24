using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The OS reduce-motion setting as capture mode wants it: always off, so every run draws the same frames.</summary>
internal sealed class StillMotionPreference : IMotionPreference
{
    public bool ReduceMotion => false;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}
