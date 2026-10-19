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
public sealed class ScopeWithdrawStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeWithdrawStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.Withdraw;

    protected override string ExpectedHash => "sha256:f4ee57b7ed70b510bc06e7a84594387159a8f69edb8e4da4a660aca30e991749";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeWithdrawStep(new InMemoryScopeRunProposals()).Run(ScopingStepContexts.Of("step.scope.withdraw", []), cancellationToken);
}
