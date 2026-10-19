using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The holdout Step of the line's prove station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveHoldoutStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ProveSteps.Holdout;

    protected override string ExpectedHash => "sha256:43fc79fc61dc312cc8f64b491fa041cc557b55c8da348c6e77ea915933224837";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
