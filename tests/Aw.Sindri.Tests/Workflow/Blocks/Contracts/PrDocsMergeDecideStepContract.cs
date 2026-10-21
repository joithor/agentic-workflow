using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrDocsMergeDecideStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.DocsMergeDecide;

    protected override string ExpectedHash => "sha256:1ad7690ea821ef93aea8ca3d21af82ba237b027cfb4779d83cb760c720c90a8a";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
