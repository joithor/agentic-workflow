using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class OwnerDecideStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => OwnerSteps.Decide;

    protected override string ExpectedHash => "sha256:8df7dfe0c17e2b53dc326b5dc77352b48520495c3e64ec3d52e12e662992237b";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
