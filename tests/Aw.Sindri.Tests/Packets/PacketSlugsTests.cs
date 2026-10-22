using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The instance segment of a scoping freeze (aw-packets' PacketId: 1-64 of lower-case letters, digits and '-', starting with a letter or digit).</summary>
[Lane(TestLane.Unit)]
public sealed class PacketSlugsTests
{
    [Theory]
    [InlineData("T-1/scoping/1", "t-1-scoping-1")]
    [InlineData("ITM-0123456789abcdef0123456789abcdef/scoping/2", "itm-0123456789abcdef0123456789abcdef-scoping-2")]
    [InlineData("//Odd  Address//", "odd-address")]
    [InlineData("///", "x")]
    public void An_address_becomes_a_lower_case_slug_of_letters_digits_and_dashes(string address, string expected) =>
        Assert.Equal(expected, PacketSlugs.InstanceSlug(address));

    [Fact]
    public void A_long_address_is_cut_at_64_characters_without_a_trailing_dash()
    {
        var slug = PacketSlugs.InstanceSlug(new string('a', 63) + "/b/c");

        Assert.Equal(new string('a', 63), slug);
    }

    [Fact]
    public void The_repo_slug_is_sindri_and_the_repo_and_null_without_one() =>
        Assert.Equal(("sindri/demo", null), (PacketSlugs.RepoSlug(RepoName.From("demo")), PacketSlugs.RepoSlug(repo: null)));

    [Fact]
    public void The_default_slug_is_cut_at_40_characters() =>
        Assert.Equal(new string('a', 40), PacketSlugs.Slug(new string('a', 50)));
}
