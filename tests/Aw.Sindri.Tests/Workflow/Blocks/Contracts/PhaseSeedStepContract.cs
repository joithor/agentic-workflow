using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Worktrees;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The phase's seed Step on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseSeedStepContract : BlockContract
{
    protected override Type Block => typeof(PhaseSeedStep);

    protected override BlockDescriptor Descriptor => PhaseSteps.Seed;

    protected override string ExpectedHash => "sha256:94b70147dc6b9d3da9fe931ea8d97abb81b1e0833238fe889481eb209e51b4cf";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PhaseSeedStep(new FakeWorktrees(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.phase.seed", []), cancellationToken);
}
