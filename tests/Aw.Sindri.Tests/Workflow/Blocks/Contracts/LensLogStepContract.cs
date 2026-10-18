using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class LensLogStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => LensRoundSteps.Log;

    protected override string ExpectedHash => "sha256:29b0a0f290bc739ccd510df2775dd46e9a211c337ad02ba07c5481d9338b4f66";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
