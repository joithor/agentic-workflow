using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrBaseIsDefaultGateContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrGates.BaseIsDefault;

    protected override string ExpectedHash => "sha256:f2ca5e4d3f12a9b90f750f5cfc110213f1ae932bf7da2914e61922d343d543b2";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
