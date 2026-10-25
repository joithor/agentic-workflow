using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

/// <summary>The folder chooser port: the in-memory fake answers what a test queued, in order; a head with none answers nothing and says it is not there.</summary>
[Lane(TestLane.Unit)]
public sealed class FolderPickerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_fake_answers_the_queued_folders_in_order_then_nothing_and_remembers_the_titles()
    {
        var picker = new FakeFolderPicker();
        picker.Queue("/work/a");
        picker.Queue(folder: null);

        var answers = new[] { await picker.Pick("first", Token), await picker.Pick("second", Token), await picker.Pick("third", Token) };

        Assert.Equal(["/work/a", null, null], answers);
        Assert.Equal(["first", "second", "third"], picker.Asked);
    }

    [Fact]
    public async Task A_head_with_no_chooser_is_unavailable_and_chooses_nothing()
    {
        var none = NullFolderPicker.Instance;
        var fake = new FakeFolderPicker { IsAvailable = false };
        fake.Queue("/work/a");

        Assert.Equal((false, false), (none.IsAvailable, fake.IsAvailable));
        Assert.Equal((null, null), (await none.Pick("x", Token), await fake.Pick("x", Token)));
    }
}
