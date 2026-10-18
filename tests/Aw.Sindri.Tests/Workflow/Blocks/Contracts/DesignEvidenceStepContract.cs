using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignEvidenceStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => DesignSteps.Evidence;

    protected override string ExpectedHash => "sha256:96e92df40d2c3c747d570e60375c669f846df3baeb87496ce1bcc4e561385fd7";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
