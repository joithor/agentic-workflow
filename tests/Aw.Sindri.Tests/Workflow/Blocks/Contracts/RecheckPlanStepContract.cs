using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class RecheckPlanStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => RecheckSteps.Plan;

    protected override string ExpectedHash => "sha256:d89745985e6c9ccf44d99796258359e7e680af9e3f1a5c82bae4aa36a6c73c10";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
