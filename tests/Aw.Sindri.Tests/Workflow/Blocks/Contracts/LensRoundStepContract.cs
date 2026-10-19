using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class LensRoundStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => LensRoundSteps.Round;

    protected override string ExpectedHash => "sha256:e0821b51b315b90745439d44725bd3d10e99a2a4b90a47b8b8d9d7b1fc44a74d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
