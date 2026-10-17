using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceDbPlanStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => EvidenceSteps.DbPlan;

    protected override string ExpectedHash => "sha256:2a7a0b5fc3bf5d2d73dc01e234d5166ba0ffc29bdf6faaeb409bb3ddc198e1d5";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
