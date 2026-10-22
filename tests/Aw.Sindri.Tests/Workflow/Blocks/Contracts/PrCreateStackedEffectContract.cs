using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

[Lane(TestLane.Unit)]
public sealed class PrCreateStackedEffectContract : BlockContract
{
    protected override Type Block => typeof(PrCreateStackedEffect);

    protected override BlockDescriptor Descriptor => PrEffects.CreateStacked;

    protected override string ExpectedHash => "sha256:de0485b2d01399c0d28791777133144bdf0544bd9ef15a219a87b40774c79aea";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var facts = new InMemoryFactStore();
        _ = await new PrCreateStackedEffect(new FakePullRequests(["github.com"]), new FakeForgeRepos(), new FakeRunDiff(), facts, facts)
            .Dispatch(ShippedComposition.Effect("effect.pr.create-stacked"), cancellationToken);
    }
}
