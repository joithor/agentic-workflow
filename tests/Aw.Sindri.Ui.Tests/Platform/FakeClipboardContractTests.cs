using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class FakeClipboardContractTests : ClipboardContract
{
    protected override IClipboard Create() => new FakeClipboard();

    protected override IClipboard CreateRefusing() => new FakeClipboard { Refuse = true };

    protected override Task<string?> Held(IClipboard clipboard) => Task.FromResult(((FakeClipboard)clipboard).Text);
}
