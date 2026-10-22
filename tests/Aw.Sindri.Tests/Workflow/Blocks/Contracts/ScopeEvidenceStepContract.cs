using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeEvidenceStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeEvidenceStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.Evidence;

    protected override string ExpectedHash => "sha256:61227542a0f3a8d36746dbfba7493292720deea94f0ec774be477702ea6d4d96";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeEvidenceStep(new InMemoryRunArea(), new InMemoryPackets()).Run(ScopingStepContexts.Of("step.scope.evidence", []), cancellationToken);
}
