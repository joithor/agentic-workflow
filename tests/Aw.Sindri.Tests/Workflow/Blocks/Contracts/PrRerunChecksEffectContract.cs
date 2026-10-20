using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrRerunChecksEffectContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrEffects.RerunChecks;

    protected override string ExpectedHash => "sha256:f0bdf81897ba0334bd3a6d51de21642cc43e9982767ac0690812b29ec6c5bf34";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
