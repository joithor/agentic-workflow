using Aw.Context.Core.Packing;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Core;

public sealed class PackerTests
{
    private static PackCandidate Candidate(string section, string marker, int tokens) => new(section,
        new ResultItem("record", Node: null, marker, "text", Score: null, [], Provenance.Deterministic, Confirmed: false, Trust.Untrusted, [marker]) { Tokens = tokens },
        marker);

    [Fact, Trait("Lane", "unit")]
    public void Sections_fill_in_order_and_overflow_is_listed_as_references_only()
    {
        var packed = Packer.Pack([Candidate("records", "C1", 40), Candidate("graph", "C2", 50), Candidate("records", "C3", 30), Candidate("records", "C4", 10)], budget: 100);

        Assert.Equal(["graph", "records"], packed.Sections.Select(s => s.Name), StringComparer.Ordinal);
        Assert.Equal(["C1", "C4"], packed.Sections[1].Items.Select(i => i.Title ?? ""), StringComparer.Ordinal);
        Assert.Equal(["C3"], packed.Sections[1].More);
        Assert.Equal(100, packed.Used);
        Assert.True(packed.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void Budget_a_later_section_leaves_unused_goes_back_to_earlier_sections_that_overflowed()
    {
        var packed = Packer.Pack([Candidate("graph", "G1", 40), Candidate("graph", "G2", 40), Candidate("graph", "G3", 40), Candidate("records", "R1", 10)], budget: 100);

        Assert.Equal(["G1", "G2"], packed.Sections[0].Items.Select(i => i.Title ?? ""), StringComparer.Ordinal);
        Assert.Equal(["G3"], packed.Sections[0].More);
        Assert.Equal(90, packed.Used);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_marker_overflowing_twice_is_listed_once()
    {
        var packed = Packer.Pack([Candidate("graph", "M", 60), Candidate("graph", "M", 60)], budget: 50);

        Assert.Equal(["M"], packed.Sections.Single().More);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_item_is_never_cut_mid_way()
    {
        var packed = Packer.Pack([Candidate("graph", "C1", tokens: 101)], budget: 100);

        Assert.Empty(packed.Sections.Single().Items);
        Assert.Equal(["C1"], packed.Sections.Single().More);
        Assert.Equal(0, packed.Used);
    }

    [Fact, Trait("Lane", "unit")]
    public void Tokens_are_utf8_bytes_over_four_rounded_up()
    {
        Assert.Equal(0, TokenEstimate.Of(text: null));
        Assert.Equal(1, TokenEstimate.Of("abc"));
        Assert.Equal(2, TokenEstimate.Of("åäö"));   // 6 bytes
    }
}
