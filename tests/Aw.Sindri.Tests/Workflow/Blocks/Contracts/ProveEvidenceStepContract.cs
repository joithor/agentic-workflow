using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The evidence Step of the line's prove station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveEvidenceStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ProveSteps.Evidence;

    protected override string ExpectedHash => "sha256:83e90ae94716b9b0b2ae681483adc3f6ea1ece5d5ebf357d9ee26f0ac09e080d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
