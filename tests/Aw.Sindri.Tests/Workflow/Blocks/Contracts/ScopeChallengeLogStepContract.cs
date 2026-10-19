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
public sealed class ScopeChallengeLogStepContract : BlockContract
{
    protected override Type Block => typeof(ChallengeLogStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.ChallengeLog;

    protected override string ExpectedHash => "sha256:5b31d3b3bcad028a6d982a27491205750bcab3ec4225b0a1265a13c9d77809e5";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ChallengeLogStep().Run(ScopingStepContexts.Of("step.scope.challenge-log", []), cancellationToken);
}
