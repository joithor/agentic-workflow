using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

[Lane(TestLane.Unit)]
public sealed class FakeFilePickerContractTests : FilePickerContract
{
    protected override IFilePicker Chooses(string name, byte[] content)
    {
        var picker = new FakeFilePicker();
        picker.Queue(new PickedFile(name, content.Length, content));
        return picker;
    }

    protected override IFilePicker ChoosesNothing() => new FakeFilePicker();
}
