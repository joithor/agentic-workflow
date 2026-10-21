using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrNextCommentStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.NextComment;

    protected override string ExpectedHash => "sha256:3ce37975a54990a4d8ef250b002c370fdaad27efca96a43840a5c2c8b2dd9e12";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
