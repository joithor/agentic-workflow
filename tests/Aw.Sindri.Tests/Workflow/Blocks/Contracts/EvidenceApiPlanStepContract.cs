using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceApiPlanStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => EvidenceSteps.ApiPlan;

    protected override string ExpectedHash => "sha256:6569f1b231e81b1c2766f6d6a2c921d913902f8bb018397ef406eb561a925988";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
