using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class BudgetAvailableGateContract : BlockContract
{
    protected override Type Block => typeof(BudgetAvailableGate);

    protected override BlockDescriptor Descriptor => BudgetAvailableGate.Descriptor;

    protected override string ExpectedHash => "sha256:2e11428fb8765eb8e43f62db6dce8dd654cddd1b6685e6c61080037174408135";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new BudgetAvailableGate().Evaluate(
            new GateContext(BlockId.From("gate.budget.available"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), Threshold: null),
            cancellationToken);
}
