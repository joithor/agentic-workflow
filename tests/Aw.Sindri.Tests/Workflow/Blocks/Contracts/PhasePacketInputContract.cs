using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PhasePacketInputContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrInputs.PhasePacket;

    protected override string ExpectedHash => "sha256:e9bda21ca9a0fd38048269de944e1e4e27974cd499f018c7823a5a49bc1a7104";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
