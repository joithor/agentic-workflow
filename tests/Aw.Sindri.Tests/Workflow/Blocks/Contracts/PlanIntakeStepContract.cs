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

    protected override string ExpectedHash => "sha256:9c9f38af5e0fb0966cf75ed63fa528c501bd6a568020490b11868ac22e5d0e48";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
