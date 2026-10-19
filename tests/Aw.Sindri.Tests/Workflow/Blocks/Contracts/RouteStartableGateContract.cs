using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The route stub (engine P7): a real gate over the approved profile, with the descriptor pinned when it was a descriptor only.</summary>
[Lane(TestLane.Unit)]
public sealed class RouteStartableGateContract : BlockContract
{
    protected override Type Block => typeof(RouteStartableGate);

    protected override BlockDescriptor Descriptor => RouteStartableGate.Descriptor;

    protected override string ExpectedHash => "sha256:00a724a0231157eba73e8b90950bde6e40fb9e6f17f03f955d3896e6aa2fadf0";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new RouteStartableGate(new InMemoryProfiles()).Evaluate(
            new GateContext(BlockId.From("gate.route.startable"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null),
            cancellationToken);
}
