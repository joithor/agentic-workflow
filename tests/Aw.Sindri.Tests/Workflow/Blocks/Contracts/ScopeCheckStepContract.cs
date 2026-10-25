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
public sealed class ScopeCheckStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeCheckStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.Check;

    protected override string ExpectedHash => "sha256:d6e0476a8fc9ae6367ce832524582d73a686794f41deef6f5799829dca161a10";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeCheckStep(new InMemoryRunArea()).Run(ScopingStepContexts.Of("step.scope.check", []), cancellationToken);
}
