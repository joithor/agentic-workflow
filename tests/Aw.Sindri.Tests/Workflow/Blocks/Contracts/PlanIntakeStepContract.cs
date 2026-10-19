using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanIntakeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PlanSteps.Intake;

    protected override string ExpectedHash => "sha256:3486e90ceb49d09bebecd4a95dcbc270c4ee8ef97d3521c0d6e89ba6148d7b08";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
