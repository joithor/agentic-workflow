using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrSecretScanGateContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrGates.SecretScan;

    protected override string ExpectedHash => "sha256:29947b4a2e53135dc91b191d90fc62b7ff5d4ff203cf3812b28e174984af6192";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
