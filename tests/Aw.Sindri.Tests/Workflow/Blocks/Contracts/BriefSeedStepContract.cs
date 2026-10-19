using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class BriefSeedStepContract : BlockContract
{
    protected override Type Block => typeof(BriefSeedStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.BriefSeed;

    protected override string ExpectedHash => "sha256:082179dd9bdf0756ffc2efb58c7ee0ccfbe9c6c259d060af83def5f348070740";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new BriefSeedStep().Run(ScopingStepContexts.Of("step.brief.seed", []), cancellationToken);
}
