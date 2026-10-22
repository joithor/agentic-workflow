using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The terminal marks cleanup: it hashes, kinds, registers once and resolves nothing here.</summary>
[Lane(TestLane.Unit)]
public sealed class PrPurgeMarksEffectContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrEffects.PurgeMarks;

    protected override string ExpectedHash => "sha256:9a6058e994caeed89a7b4e67887ab92f2a04dd8d436aa5d4fd701a150f07a33e";

    protected override bool HasRuntime => true;

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
