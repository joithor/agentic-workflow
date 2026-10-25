using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanHoldoutStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PlanSteps.Holdout;

    protected override string ExpectedHash => "sha256:e81f65a6acbf05549dc5c3e58ab2b077b5b10b52f27b3d04aaded977fb40618d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
