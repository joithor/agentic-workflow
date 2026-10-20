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

    protected override string ExpectedHash => "sha256:c62217b7dd6c9cb27178d62981325f6d3d1c39319a0f0ec7185ee00cd33da719";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new DocsCheckStep(new FakeRunDiff(), new InMemoryBriefFiles()).Run(BuildShipContexts.Of("step.docs.check", []), cancellationToken);
}
