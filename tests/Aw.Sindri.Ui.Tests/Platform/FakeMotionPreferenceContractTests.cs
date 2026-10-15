using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class FakeMotionPreferenceContractTests : MotionPreferenceContract
{
    protected override IMotionPreference Create(bool reduce)
    {
        var motion = new FakeMotionPreference();
        motion.Set(reduce);
        return motion;
    }

    protected override void Change(IMotionPreference preference, bool reduce) => ((FakeMotionPreference)preference).Set(reduce);
}
