using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeReadTrackerStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeReadTrackerStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.ReadTracker;

    protected override string ExpectedHash => "sha256:1e767ceeda8672f4840d2314115a40d6f140481de25813cf487fc10bf82c1d4b";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeReadTrackerStep(InMemoryTrackerSource.Unconfigured, new InMemoryScopingTextShape([]), new PassThroughScrubber(), TimeProvider.System).Run(ScopingStepContexts.Of("step.scope.read-tracker", []), cancellationToken);
}
