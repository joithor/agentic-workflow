using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class RouteStartableGateContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PendingRuntimes.RouteStartable;

    protected override string ExpectedHash => "sha256:00a724a0231157eba73e8b90950bde6e40fb9e6f17f03f955d3896e6aa2fadf0";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
