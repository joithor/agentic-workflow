using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class FakeLauncherContractTests : LauncherContract
{
    protected override ILauncher Create() => new FakeLauncher();

    protected override ILauncher CreateRefusing() => new FakeLauncher { Refuse = true };

    protected override string ExistingFile(ILauncher launcher)
    {
        var path = Path.Combine(Path.GetTempPath(), "prd.md");
        ((FakeLauncher)launcher).AddFile(path);
        return path;
    }
}
