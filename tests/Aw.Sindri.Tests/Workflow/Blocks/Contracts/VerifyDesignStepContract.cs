using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class VerifyDesignStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ProofSteps.VerifyDesign;

    protected override string ExpectedHash => "sha256:f64255db776db059a66d31bf1234d42fde46a2ebb7b2fb0568c21ac3ec44cdd5";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
