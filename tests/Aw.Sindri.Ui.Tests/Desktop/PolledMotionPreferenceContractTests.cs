using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The polled adapter over a source this binding drives (the OS setting itself is never touched).</summary>
[Lane(TestLane.Unit)]
public sealed class PolledMotionPreferenceContractTests : MotionPreferenceContract, IDisposable
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(2);
    private readonly FakeTimeProvider _clock = new();
    private readonly List<PolledMotionPreference> _made = [];
    private bool _source;

    public void Dispose() => _made.ForEach(m => m.Dispose());

    protected override IMotionPreference Create(bool reduce)
    {
        _source = reduce;
        var motion = new PolledMotionPreference(() => _source, _clock, _interval);
        _made.Add(motion);
        return motion;
    }

    protected override void Change(IMotionPreference preference, bool reduce)
    {
        _source = reduce;
        _clock.Advance(_interval);
    }

    [Fact]
    public void A_read_that_throws_on_a_tick_keeps_the_last_value_and_raises_nothing()
    {
        var fail = false;
        using var motion = new PolledMotionPreference(() => fail ? throw new FileNotFoundException("cwd is gone") : true, _clock, _interval);
        var changes = 0;
        motion.Changed += (_, _) => changes++;
        fail = true;

        _clock.Advance(_interval);
        _clock.Advance(_interval);

        Assert.True(motion.ReduceMotion);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void A_first_read_that_throws_reads_as_no_reduction()
    {
        using var motion = new PolledMotionPreference(() => throw new FileNotFoundException("cwd is gone"), _clock, _interval);

        Assert.False(motion.ReduceMotion);
    }

    [Fact]
    public void A_read_that_succeeds_after_failures_changes_the_value_once()
    {
        var fail = true;
        using var motion = new PolledMotionPreference(() => fail ? throw new IOException("cwd is gone") : true, _clock, _interval);
        var changes = 0;
        motion.Changed += (_, _) => changes++;

        _clock.Advance(_interval);
        _clock.Advance(_interval);
        fail = false;
        _clock.Advance(_interval);
        _clock.Advance(_interval);

        Assert.True(motion.ReduceMotion);
        Assert.Equal(1, changes);
    }
}
