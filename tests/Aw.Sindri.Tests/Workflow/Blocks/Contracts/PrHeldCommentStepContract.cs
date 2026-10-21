using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrHeldCommentStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.HeldComment;

    protected override string ExpectedHash => "sha256:6f9d10a0df09d786d3be1db3cecd73f2d41284b476e44dd38c7132c1fcae70c5";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
