using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrPushEffectContract : BlockContract
{
    protected override Type Block => typeof(PrPushEffect);

    protected override BlockDescriptor Descriptor => PrEffects.Push;

    protected override string ExpectedHash => "sha256:39b2837b3cc1ce17ba5be44ed2275f4d33cf8b5c23b3afa545d2414fd688ad0f";   // PENDING: the descriptor now lists fact:push in Outputs, so the hash changes; replace with the value the contract test prints

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrPushEffect(new FakePullRequests([]), new FakeRunDiff(), new InMemoryBranchChanges(), new InMemoryFactStore(), new InMemoryFactStore(), new MergeRecord(new InMemoryRunArea(), TimeProvider.System))
            .Dispatch(ShippedComposition.Effect("effect.pr.push"), cancellationToken);
}
