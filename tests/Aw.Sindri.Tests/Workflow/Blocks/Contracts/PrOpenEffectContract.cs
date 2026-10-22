using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrOpenEffectContract : BlockContract
{
    protected override Type Block => typeof(PrOpenEffect);

    protected override BlockDescriptor Descriptor => PrOpenEffect.Descriptor;

    protected override string ExpectedHash => "sha256:81179b37fa1a2a9456a4f7b3e78b68f2d54ccb2a48ad52c995ea7a3ff9080b16";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var facts = new InMemoryFactStore();
        _ = await new PrOpenEffect(new FakePullRequests(["github.com"]), new FakeForgeRepos(), new InMemoryPacketFiles(), facts, facts, new InMemoryProfiles())
            .Dispatch(ShippedComposition.Effect("effect.pr.open"), cancellationToken);
    }
}
