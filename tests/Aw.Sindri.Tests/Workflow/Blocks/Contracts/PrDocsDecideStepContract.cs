using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrDocsDecideStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.DocsDecide;

    protected override string ExpectedHash => "sha256:78b9bb4be67859d025392afb585eb7e76b6a6b50bd8e8a1e3d239f5495dd7a27";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
