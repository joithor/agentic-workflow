using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewApplyAnswerStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ReviewSteps.ApplyAnswer;

    protected override string ExpectedHash => "sha256:947b31ab738fa2fd8a680dc1e26afc57a56616c7841f4040c5af15d87fff9ec7";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
