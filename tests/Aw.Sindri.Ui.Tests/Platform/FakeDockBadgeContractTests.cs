using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class FakeDockBadgeContractTests : DockBadgeContract
{
    protected override IDockBadge Create() => new FakeDockBadge();
}
