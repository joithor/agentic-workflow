using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.Tests.Controls;

/// <summary>The typed confirmation (setup spec §2.1): the phrase is the server's text, and only exactly it presses the button.</summary>
[Lane(TestLane.Unit)]
public sealed class TypedConfirmBoxTests
{
    private readonly AppConfirmFixture _f = new();

    [Theory]
    [InlineData("approve profile 3f9a1c", true)]
    [InlineData("  approve profile 3f9a1c  ", true)]
    [InlineData("Approve profile 3f9a1c", false)]
    [InlineData("approve profile 3f9a1", false)]
    [InlineData("approve profile 3f9a1cc", false)]
    [InlineData("", false)]
    public void Only_the_exact_phrase_enables_the_button(string typed, bool enabled)
    {
        var box = new TypedConfirmBoxViewModel("ApproveProfile", _f.Catalog(), _f.Clock);
        box.Offer(_f.Typed());

        box.Text = typed;

        Assert.Equal(enabled, box.CanSubmit);
    }
}
