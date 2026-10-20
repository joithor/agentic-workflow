using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Images;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Design;

public sealed class GeometryAndParityTests
{
    private static readonly ElementSelector _button = ElementSelector.ByTestId(TestId.From("button"));
    private static readonly ElementSelector _header = ElementSelector.ByText("Header");

    private static Func<ElementSelector, Box?> Measured(params (ElementSelector Selector, Box Box)[] boxes) =>
        s => boxes.FirstOrDefault(b => b.Selector.Key == s.Key).Box;

    [Fact, Trait("Lane", "unit")]
    public void A_box_within_tolerance_passes()
    {
        var check = GeometryChecks.Check([new BoxExpectation(_button, new ExpectedGeometry(W: 40, H: 20), Tolerance: 1)], Measured((_button, new Box(5, 5, 40.6, 19.5))))[0];

        Assert.True(check.Pass);
        Assert.Null(check.Failure);
        Assert.Equal(new MeasuredGeometry(40.6, 19.5, 5, 5), check.Actual);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_axis_outside_tolerance_is_named()
    {
        var check = GeometryChecks.Check([new BoxExpectation(_button, new ExpectedGeometry(W: 40, X: 0))], Measured((_button, new Box(3, 0, 42, 10))))[0];

        Assert.False(check.Pass);
        Assert.Equal("w 42 != 40, x 3 != 0", check.Failure);
    }

    [Fact, Trait("Lane", "unit")]
    public void Positions_are_measured_from_the_relative_element()
    {
        var check = GeometryChecks.Check(
            [new BoxExpectation(_button, new ExpectedGeometry(X: 10, Y: 30), _header)],
            Measured((_button, new Box(110, 230, 40, 20)), (_header, new Box(100, 200, 500, 30))))[0];

        Assert.True(check.Pass);
        Assert.Equal("text:Header", check.RelativeTo);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_element_fails_and_is_never_skipped()
    {
        var checks = GeometryChecks.Check(
            [new BoxExpectation(_button, new ExpectedGeometry(W: 1)), new BoxExpectation(_header, new ExpectedGeometry(W: 1), _button)],
            Measured((_header, new Box(0, 0, 1, 1))));

        Assert.Equal(["testId:button not found", "testId:button not found"], checks.Select(static c => c.Failure), StringComparer.Ordinal);
        Assert.All(checks, static c => Assert.Null(c.Actual));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_implementation_crop_is_the_anchor_origin_plus_offset_at_design_size() =>
        Assert.Equal(new Region(13, 18, 40, 30), ParityRegions.Implementation(new Box(10.4, 20.5, 99, 99), new Region(0, 0, 40, 30), new Offset(2.2, -2.5)));

    [Fact, Trait("Lane", "unit")]
    public void A_region_past_the_viewport_names_the_viewport_it_needs()
    {
        Assert.Null(ParityRegions.Shortfall(new Region(0, 0, 100, 100), new ViewportSize(100, 100)));
        Assert.Equal(new ViewportSize(150, 100), ParityRegions.Shortfall(new Region(100, 0, 50, 10), new ViewportSize(100, 100)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_measured_frame_becomes_its_parity_entry()
    {
        var frame = ManifestBuilder.Frame("empty") with { AnchorOffset = new Offset(1, 2), KnownDifferences = ["icon"], DesignNode = "1:2" };
        var comparison = new PixelComparison(3, 100, RgbaImage.Filled(1, 1, 0xFFFFFFFF), Changed: null);
        var measurement = new ParityMeasurement(frame, new Box(4.333, 5, 12.346, 9), comparison, [], ParityFiles.For("/run/p1", "empty"));

        var entry = measurement.ToFrame(A.Manifest().Build());

        Assert.Equal(new Offset(5, 7), entry.ImplementationOrigin);
        Assert.Equal(new SizeDelta(2.35, -1), entry.SizeDelta);
        Assert.Equal(3, entry.DiffPercent);
        Assert.Equal(0.1, entry.Threshold);
        Assert.Equal("/run/p1/empty-side-by-side.png", entry.Files.SideBySide);
        Assert.Equal("1:2", entry.DesignNode);
    }
}
