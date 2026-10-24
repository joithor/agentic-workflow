using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The one table that says which band a node kind sits in (q86, owner q95: the ui owns the mapping), and the style each kind is drawn in.</summary>
[Lane(TestLane.Unit)]
public sealed class NodeBandsTests
{
    [Theory]
    [InlineData("record", WholeBand.Intent)]
    [InlineData("decision", WholeBand.Intent)]
    [InlineData("feature", WholeBand.Product)]
    [InlineData("surface", WholeBand.Product)]
    [InlineData("action", WholeBand.Product)]
    [InlineData("thing", WholeBand.Product)]
    [InlineData("flow", WholeBand.Product)]
    [InlineData("code", WholeBand.Code)]
    [InlineData("something-new", WholeBand.Other)]
    [InlineData("", WholeBand.Other)]
    public void A_kind_sits_in_the_band_the_table_gives_it(string kind, WholeBand band) => Assert.Equal(band, NodeBands.Of(kind));

    [Fact]
    public void The_bands_run_why_then_product_then_code_then_other()
    {
        Assert.Equal([WholeBand.Intent, WholeBand.Product, WholeBand.Code, WholeBand.Other], Enum.GetValues<WholeBand>());
    }

    [Fact]
    public void Every_kind_of_the_table_has_a_style_drawn_from_theme_tokens_and_no_two_kinds_of_a_band_look_alike()
    {
        foreach (var kind in NodeBands.Kinds)
        {
            Assert.StartsWith("Brush.", NodeBands.StyleOf(kind).Accent, StringComparison.Ordinal);
        }

        foreach (var band in new[] { WholeBand.Intent, WholeBand.Product })
        {
            var looks = NodeBands.Kinds.Where(k => NodeBands.Of(k) == band).Select(k => NodeBands.StyleOf(k)).Select(s => (s.Shape, s.Mark)).ToList();
            Assert.Equal(looks.Count, looks.Distinct().Count());
        }
    }

    [Fact]
    public void Intent_is_a_pill_product_a_card_and_code_a_plate()
    {
        Assert.Equal(ChipShape.Pill, NodeBands.StyleOf("decision").Shape);
        Assert.Equal(ChipShape.Card, NodeBands.StyleOf("surface").Shape);
        Assert.Equal(ChipShape.Plate, NodeBands.StyleOf("code").Shape);
    }

    [Fact]
    public void An_unknown_kind_still_has_a_style()
    {
        Assert.StartsWith("Brush.", NodeBands.StyleOf("something-new").Accent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_band_is_summed_up_in_its_kinds_biggest_first()
    {
        var meta = NodeBands.Meta([new("decision", 1), new("record", 3)]);

        Assert.Equal("3 records · 1 decision", meta);
    }
}
