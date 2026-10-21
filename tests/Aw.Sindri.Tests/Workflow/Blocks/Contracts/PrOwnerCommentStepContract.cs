using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrOwnerCommentStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.OwnerComment;

    protected override string ExpectedHash => "sha256:96b3c7f85a0c7372f7272bb5c37fb0159b794eb344fabc0c4d248b83a0ba3e38";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
