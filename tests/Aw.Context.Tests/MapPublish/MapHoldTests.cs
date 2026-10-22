using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.MapPublish;

public sealed class MapHoldTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-10-20T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact, Trait("Lane", "unit")]
    public void With_no_chain_the_wait_starts_now() => Assert.Equal(_now, MapHold.NextSince(chain: null, _now));

    [Fact, Trait("Lane", "unit")]
    public void A_wait_under_way_keeps_its_start() => Assert.Equal(_now.AddMinutes(-9), MapHold.NextSince(_now.AddMinutes(-9), _now));

    [Fact, Trait("Lane", "unit")]
    public void A_start_later_than_now_is_clamped_to_now_so_a_clock_stepped_back_cannot_lengthen_the_wait()
    {
        Assert.Equal(_now, MapHold.NextSince(_now.AddHours(2), _now));
        Assert.Equal(_now, MapHold.NextSince(_now, _now));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("abcdef0", true)]
    [InlineData("0123456789abcdef0123456789abcdef01234567", true)]
    [InlineData("abcdef", false)]
    [InlineData("0123456789abcdef0123456789abcdef012345678", false)]
    [InlineData("ABCDEF0", false)]
    [InlineData("abcdefg", false)]
    [InlineData("abcdef0\n", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void A_commit_is_7_to_40_lowercase_hex(string? text, bool valid) => Assert.Equal(valid, MapHead.IsCommit(text));

    [Fact, Trait("Lane", "unit")]
    public void Heads_are_checked_for_their_commit_and_for_a_repo_named_twice()
    {
        var repo = ContextRepoName.From("aw-split-panes");

        Assert.Null(MapHead.Check([new MapHead(repo, "abcdef0", OnDefault: true)]));
        Assert.Equal("SND-CONTEXT-191", MapHead.Check([new MapHead(repo, "nope", OnDefault: true)])!.Code);
        Assert.Equal("SND-CONTEXT-191", MapHead.Check([new MapHead(repo, "abcdef0", OnDefault: true), new MapHead(repo, "abcdef1", OnDefault: true)])!.Code);
    }
}
