using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanEvidenceStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PlanSteps.Evidence;

    protected override string ExpectedHash => "sha256:601f0d8d8a97e571a59bac477718ca59ede5fe052f2a8f56e1cd6474ab3c6f9c";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
