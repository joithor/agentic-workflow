using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeReadBriefStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeReadBriefStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.ReadBrief;

    protected override string ExpectedHash => "sha256:34ed392d91e7189a9219fd6f9f586f716dc76368ef52f3b0192440a7dcf3e200";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeReadBriefStep(new InMemoryBriefFiles(), new InMemoryRepoCheckouts(), new InMemoryScopingTextShape([]), TimeProvider.System).Run(ScopingStepContexts.Of("step.scope.read-brief", []), cancellationToken);
}
