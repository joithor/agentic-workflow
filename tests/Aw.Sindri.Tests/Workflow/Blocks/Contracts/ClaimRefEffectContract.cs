using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class ClaimRefEffectContract : BlockContract
{
    protected override Type Block => typeof(ClaimRefEffect);

    protected override BlockDescriptor Descriptor => ClaimRefEffect.Descriptor;

    protected override string ExpectedHash => "sha256:2a44426d862b3721014969dfcc03a7851f56551f2b9bec9f340e500353a9abd3";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ClaimRefEffect(new InMemoryWorkflowClaims(), new FakeInstanceLiveness(), new GitClaimRefs(), new InMemoryFactStore(), new InMemoryProfiles())
            .Dispatch(ShippedComposition.Effect("effect.claim.ref"), cancellationToken);
}
