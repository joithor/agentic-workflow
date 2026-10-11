using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Lint;

namespace Aw.Evidence.Tests.Lint;

public sealed class PageLintTests
{
    private static readonly ElementStyle _plain = new(TextOverflow: "clip", Overflow: "visible", Color: "rgb(0, 0, 0)");

    private static PageElement El(string selector, Box rect, string text = "ok", ElementStyle? style = null, bool hover = true, bool focus = true) =>
        new(selector, rect, text, style ?? _plain, hover, focus);

    private static IReadOnlyList<LintRule> Rules(params PageElement[] elements) =>
        [.. PageLint.Check(new PageSnapshot(elements, new ViewportSize(375, 812))).Select(static f => f.Rule)];

    [Fact, Trait("Lane", "unit")]
    public void Overlapping_boxes_are_flagged_touching_ones_are_not()
    {
        Assert.Equal([LintRule.Overlap], Rules(El("div#a", new(0, 0, 50, 50)), El("div#b", new(40, 40, 50, 50))));
        Assert.Empty(Rules(El("div#a", new(0, 0, 50, 50)), El("div#b", new(50, 0, 50, 50))));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void Left_edges_one_to_four_px_apart_are_misaligned(double delta, bool flagged) =>
        Assert.Equal(flagged, Rules(El("div#a", new(10, 0, 20, 20)), El("div#b", new(10 + delta, 100, 20, 20))).Contains(LintRule.MisalignedEdge));

    [Fact, Trait("Lane", "unit")]
    public void An_element_past_the_viewport_edge_overflows() =>
        Assert.Equal([LintRule.PhoneOverflow], Rules(El("div#wide", new(300, 0, 100, 10))));

    [Fact, Trait("Lane", "unit")]
    public void A_long_label_cut_by_an_ellipsis_is_truncated()
    {
        var ellipsis = _plain with { TextOverflow = "ellipsis", Overflow = "hidden" };

        Assert.Equal([LintRule.TruncatedLabel], Rules(El("span#l", new(0, 0, 400 - 100, 20), new string('x', 21), ellipsis)));
        Assert.Empty(Rules(El("span#l", new(0, 0, 300, 20), new string('x', 20), ellipsis)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Hidden_overflow_with_text_wider_than_the_box_is_clipped()
    {
        var hidden = _plain with { Overflow = "hidden" };

        Assert.Equal([LintRule.Clipped], Rules(El("span#c", new(0, 0, 69, 20), "ten chars!", hidden)));
        Assert.Empty(Rules(El("span#c", new(0, 0, 70, 20), "ten chars!", hidden)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("button#save", true)]
    [InlineData("a#home", true)]
    [InlineData("input#q", true)]
    [InlineData("[role=button]", true)]
    [InlineData("div#card", false)]
    [InlineData("abbr#x", false)]
    public void Interactive_elements_need_hover_and_focus_styles(string selector, bool flagged) =>
        Assert.Equal(flagged, Rules(El(selector, new(0, 0, 10, 10), hover: false)).Contains(LintRule.MissingHoverFocus));

    [Theory, Trait("Lane", "unit")]
    [InlineData("Loading...", true)]
    [InlineData("  loading ", true)]
    [InlineData("undefined", true)]
    [InlineData("Total: NaN", true)]
    [InlineData("Owner: null", true)]
    [InlineData("Nullable fields", false)]
    [InlineData("Loading complete", false)]
    public void Unresolved_placeholders_are_flagged(string text, bool flagged) =>
        Assert.Equal(flagged, Rules(El("div#t", new(0, 0, 300, 20), text)).Contains(LintRule.UnresolvedPlaceholder));

    [Fact, Trait("Lane", "unit")]
    public void Colours_are_checked_only_against_a_given_allow_list()
    {
        var snapshot = new PageSnapshot([El("p#a", new(0, 0, 10, 10)), El("p#b", new(0, 100, 10, 10), style: _plain with { Color = "rgb(1, 2, 3)" })], new ViewportSize(375, 812));

        Assert.DoesNotContain(PageLint.Check(snapshot), static f => f.Rule == LintRule.NonTokenColor);
        var finding = Assert.Single(PageLint.Check(snapshot, ["RGB(0, 0, 0)"]), static f => f.Rule == LintRule.NonTokenColor);
        Assert.Equal("p#b", finding.Selector);
    }
}
