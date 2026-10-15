using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.Tests.Navigation;

/// <summary>UI spec §3.3 "Deep links inside the app".</summary>
[Lane(TestLane.Unit)]
public sealed class DeepLinkTests
{
    [Theory]
    [InlineData("home/needs-you", nameof(DeepLinkKind.HomeNeedsYou), null)]
    [InlineData("needs-you/nyr-01JC8", nameof(DeepLinkKind.NeedsYou), "nyr-01JC8")]
    [InlineData("item/ITM-01JC8", nameof(DeepLinkKind.Item), "ITM-01JC8")]
    [InlineData("decision/DEC-01JD2", nameof(DeepLinkKind.Decision), "DEC-01JD2")]
    [InlineData("service/index", nameof(DeepLinkKind.Service), "index")]
    [InlineData("service/nightly.index_build", nameof(DeepLinkKind.Service), "nightly.index_build")]
    [InlineData("job/JOB-01:2", nameof(DeepLinkKind.Job), "JOB-01:2")]
    public void Each_form_parses_and_prints_back(string text, string kind, string? subject)
    {
        Assert.True(DeepLink.TryParse(text, out var link));
        Assert.Equal(new DeepLink(Enum.Parse<DeepLinkKind>(kind), subject), link);
        Assert.Equal(text, link.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("focus")]
    [InlineData("home")]
    [InlineData("home/queue")]
    [InlineData("home/needs-you/x")]
    [InlineData("item")]
    [InlineData("item/")]
    [InlineData("item/a/b")]
    [InlineData("item/ITM 1")]
    [InlineData("item/../x")]
    [InlineData("Item/ITM-1")]
    [InlineData("run/ITM-1")]
    [InlineData("sindri://item/ITM-1")]
    [InlineData(" item/ITM-1")]
    [InlineData("item/ITM-1\n")]
    public void Anything_else_is_not_a_link(string? text)
    {
        Assert.False(DeepLink.TryParse(text, out var link));
        Assert.Null(link);
    }

    [Fact]
    public void A_subject_longer_than_128_is_not_a_link()
    {
        Assert.True(DeepLink.TryParse("item/" + new string('a', 128), out _));
        Assert.False(DeepLink.TryParse("item/" + new string('a', 129), out _));
    }
}
