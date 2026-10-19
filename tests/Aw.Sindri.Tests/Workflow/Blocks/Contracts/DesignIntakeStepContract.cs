using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignIntakeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => DesignSteps.Intake;

    protected override string ExpectedHash => "sha256:b0c4f38d67e08b76df6a95f8317542d801e458ca58e6c2a31d97b9cb91ec62ed";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
