using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignLensDeskStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => DesignSteps.LensDesk;

    protected override string ExpectedHash => "sha256:884a444d706a72279cd37fd36b054501348dc7fbe76b60c63aa2edb5e196d989";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
