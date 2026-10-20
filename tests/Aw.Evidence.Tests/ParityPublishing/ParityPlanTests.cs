using Aw.Evidence.Core;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Features.ParityPlan;
using Aw.Evidence.Testing;
using Aw.Kernel.Json;

namespace Aw.Evidence.Tests.ParityPublishing;

public sealed class ParityPlanTests
{
    private const string SideBySide = "/runs/parity-1/default-side-by-side.png";

    private static InMemoryRunFiles RunWith(Provenance provenance) => new InMemoryRunFiles()
        .With("/runs/parity-1/parity.json", AwJson.Serialize(A.ParitySummary().WithProvenance(provenance).Build(), TestJson.Options))
        .With(SideBySide, new byte[1234]);

    private static ParityPlanService Service(InMemoryRunFiles files) => new(files, TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public async Task ParityPlan_blocks_on_review_until_every_image_is_reviewed()
    {
        var files = RunWith(Provenance.Seeded);

        var blocked = (await Service(files).Handle(new("/runs/parity-1", []), CancellationToken.None)).Value;
        var clear = (await Service(files).Handle(new("/runs/parity-1", [SideBySide]), CancellationToken.None)).Value;

        Assert.True(blocked.ReviewBlocked);
        Assert.Equal([SideBySide], blocked.Unreviewed);
        var attachment = Assert.Single(blocked.Plan);
        Assert.Equal((1234L, "Pixel diff: default (Figma | implementation | diff)"), (attachment.Size, attachment.Title));
        Assert.True(blocked.AskFirst);
        Assert.False(clear.ReviewBlocked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ParityPlan_keeps_unknown_provenance_local()
    {
        var result = await Service(RunWith(Provenance.Unknown)).Handle(new("/runs/parity-1", []), CancellationToken.None);

        Assert.Equal(EvidenceErrors.PublishRefused.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ParityPlan_needs_a_parity_json()
    {
        var result = await Service(new InMemoryRunFiles()).Handle(new("/runs/parity-1", []), CancellationToken.None);

        Assert.Equal(EvidenceErrors.FileNotFound.Code, result.Error.Code);
    }
}
