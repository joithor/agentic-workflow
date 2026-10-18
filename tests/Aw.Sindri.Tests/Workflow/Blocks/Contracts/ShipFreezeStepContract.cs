using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipFreezeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ShipSteps.Freeze;

    protected override string ExpectedHash => "sha256:3453040d172cde0b45d927abd0eafa80b9f37fe4fcaa4c2ad67705ed5d2840f6";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
