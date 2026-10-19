using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignCheckStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => DesignSteps.Check;

    protected override string ExpectedHash => "sha256:18077dc3b7e862b03855f6a80bbb411a8ba8196fa8ed315ddfb50f5e489d418e";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
