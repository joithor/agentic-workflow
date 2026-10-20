using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewRoundStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ReviewSteps.Round;

    protected override string ExpectedHash => "sha256:2aa8ad4611e71344356bbc5baf018bcde470b692ac899503d4799dc5a3dac8a7";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
