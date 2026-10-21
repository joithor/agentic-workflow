using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrReplyDraftStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.ReplyDraft;

    protected override string ExpectedHash => "sha256:fa85ab585ec676e6c8280eb7e34ca249de006a51c6abae196a2ac7e4d3aa8cbc";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
