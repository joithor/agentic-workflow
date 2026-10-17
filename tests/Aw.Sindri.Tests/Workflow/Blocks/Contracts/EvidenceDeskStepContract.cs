using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceDeskStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => EvidenceSteps.Desk;

    protected override string ExpectedHash => "sha256:6f4e09b7fad8b4a095386eaa2083406a84245702cfb6d887a3a3d7934dcce0fe";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
