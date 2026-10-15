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
}
