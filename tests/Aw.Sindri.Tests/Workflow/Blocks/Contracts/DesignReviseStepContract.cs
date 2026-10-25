using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignReviseStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => DesignSteps.Revise;

    protected override string ExpectedHash => "sha256:8bc5a2281b87a9c1cabaaaf8351fe08caaaabb00195c3c7bdbba601ac1784091";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
