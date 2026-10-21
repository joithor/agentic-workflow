using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrFixCommentStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.FixComment;

    protected override string ExpectedHash => "sha256:c2b3226f80e8eb34bdbf8b5c22ee7279cd58337a4eed6b411e274e8baf13cee4";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
