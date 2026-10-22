using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrMergeDocsEffectContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrEffects.MergeDocs;

    protected override string ExpectedHash => "sha256:98f58bfa1c0c1605d8c8860a8928f9e89cc53f70e9848834be87c76f0c5507c9";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
