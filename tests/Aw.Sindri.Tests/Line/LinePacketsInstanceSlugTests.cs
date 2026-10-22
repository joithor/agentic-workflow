using Aw.Sindri.Features.Line;

namespace Aw.Sindri.Tests.Line;

/// <summary>The instance segment of a scoping freeze (aw-packets' PacketId: 1-64 of lower-case letters, digits and '-', starting with a letter or digit).</summary>
[Lane(TestLane.Unit)]
public sealed class LinePacketsInstanceSlugTests
{
    [Theory]
    [InlineData("T-1/scoping/1", "t-1-scoping-1")]
    [InlineData("ITM-0123456789abcdef0123456789abcdef/scoping/2", "itm-0123456789abcdef0123456789abcdef-scoping-2")]
    [InlineData("//Odd  Address//", "odd-address")]
    [InlineData("///", "x")]
    public void An_address_becomes_a_lower_case_slug_of_letters_digits_and_dashes(string address, string expected)
    {
        Assert.Equal(expected, LinePackets.InstanceSlug(address));
    }

    [Fact]
    public void A_long_address_is_cut_at_64_characters_without_a_trailing_dash()
    {
        var slug = LinePackets.InstanceSlug(new string('a', 63) + "/b/c");

        Assert.Equal(new string('a', 63), slug);
    }
}
