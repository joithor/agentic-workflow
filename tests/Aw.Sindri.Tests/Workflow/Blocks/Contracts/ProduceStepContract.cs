using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ProduceStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => DeskSteps.Produce;

    protected override string ExpectedHash => "sha256:f3641bb5fd611951f4f20b0fc6f85cbec50025d199f3eb8721d2cdd3bf2baf51";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
