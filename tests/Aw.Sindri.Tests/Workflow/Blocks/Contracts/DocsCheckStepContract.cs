using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Docs;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (BS-11): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class DocsCheckStepContract : BlockContract
{
    protected override Type Block => typeof(DocsCheckStep);

    protected override BlockDescriptor Descriptor => DocsSteps.Check;

    protected override string ExpectedHash => "sha256:86370439930195d2543e6d57da90e5e73281bde43e0f78c1c42b97929a2dc43d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new DocsCheckStep(new FakeRunDiff(), new InMemoryBriefFiles()).Run(BuildShipContexts.Of("step.docs.check", []), cancellationToken);
}
