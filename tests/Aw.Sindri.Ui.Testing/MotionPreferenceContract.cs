using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The contract every <see cref="IMotionPreference"/> passes: the fake, and each head adapter bound over a source the
/// binding can drive (an adapter takes the OS reader and change signal as a seam, so the OS setting itself is never touched).
/// </summary>
public abstract class MotionPreferenceContract
{
    /// <summary>A preference whose source starts at <paramref name="reduce"/>.</summary>
    /// <param name="reduce">The source's starting value.</param>
    /// <returns>The preference.</returns>
    protected abstract IMotionPreference Create(bool reduce);

    /// <summary>Changes the source behind <paramref name="preference"/>, as the OS does.</summary>
    /// <param name="preference">A preference from <see cref="Create"/>.</param>
    /// <param name="reduce">The new value.</param>
    protected abstract void Change(IMotionPreference preference, bool reduce);

    [Fact]
    public void It_reads_the_source()
    {
        Expect.True(Create(reduce: true).ReduceMotion);
        Expect.True(!Create(reduce: false).ReduceMotion);
    }

    [Fact]
    public void A_change_is_raised_once_and_reads_the_new_value()
    {
        var motion = Create(reduce: false);
        var raised = 0;
        motion.Changed += (_, _) => raised++;

        Change(motion, reduce: true);

        Expect.Equal(1, raised);
        Expect.True(motion.ReduceMotion);
    }

    [Fact]
    public void Setting_the_same_value_raises_nothing()
    {
        var motion = Create(reduce: true);
        var raised = 0;
        motion.Changed += (_, _) => raised++;

        Change(motion, reduce: true);

        Expect.Equal(0, raised);
    }

    [Fact]
    public void An_unsubscribed_handler_hears_nothing()
    {
        var motion = Create(reduce: false);
        var raised = 0;
        void Count(object? sender, EventArgs e) => raised++;
        motion.Changed += Count;
        motion.Changed -= Count;

        Change(motion, reduce: true);

        Expect.Equal(0, raised);
    }
}
