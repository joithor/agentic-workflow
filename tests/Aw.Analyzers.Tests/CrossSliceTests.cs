using Aw.Kernel.Testing;

namespace Aw.Analyzers.Tests;

[Lane(TestLane.Unit)]
public sealed class CrossSliceTests
{
    private const string Start = """
        namespace Aw.Probe.Features.StartItem;
        internal sealed class StartItemService
        {
            internal int M() => Aw.Probe.Features.StopItem.StopItemRules.Limit + new Aw.Probe.Core.Shared().N;
            internal Aw.Probe.Features.StopItem.StopItemRequest? Last { get; set; }
        }
        """;

    private const string Stop = """
        namespace Aw.Probe.Features.StopItem;
        internal static class StopItemRules { internal const int Limit = 3; }
        internal sealed record StopItemRequest(int N);
        internal sealed class StopItemService { internal StopItemRequest Make() => new(StopItemRules.Limit); }
        """;

    private const string Core = "namespace Aw.Probe.Core;\ninternal sealed class Shared { internal int N => 1; }\n";

    [Fact]
    public async Task Flags_a_slice_using_another_slices_types()
    {
        var found = await AnalyzerHarness.Run(
            new CrossSliceAnalyzer(), "Aw.Probe.Features", ("Start.cs", Start), ("Stop.cs", Stop), ("Core.cs", Core));

        // Start.cs: StopItemRules (line 4) and StopItemRequest (line 5). Same-slice and Core references are fine.
        Assert.Equal(["AW1003@4", "AW1003@5"], found);
    }

    [Fact]
    public async Task Applies_to_small_shape_App_features()
    {
        var found = await AnalyzerHarness.Run(
            new CrossSliceAnalyzer(),
            "Aw.Probe.App",
            ("A.cs", "namespace Aw.Probe.App.Features.A;\ninternal sealed class AService { internal int M() => Aw.Probe.App.Features.B.BRules.N; }\n"),
            ("B.cs", "namespace Aw.Probe.App.Features.B;\ninternal static class BRules { internal const int N = 1; }\n"));

        Assert.Equal(["AW1003@2"], found);
    }
}
