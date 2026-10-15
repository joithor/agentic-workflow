using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory reduce-motion setting: whatever the test sets, with a change event.</summary>
internal sealed class FakeMotionPreference : IMotionPreference
{
    public event EventHandler? Changed;

    public bool ReduceMotion { get; private set; }

    /// <summary>Changes the setting, as the OS does; raises <see cref="Changed"/> when the value differs.</summary>
    /// <param name="reduce">The new setting.</param>
    public void Set(bool reduce)
    {
        if (reduce == ReduceMotion)
        {
            return;
        }

        ReduceMotion = reduce;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
