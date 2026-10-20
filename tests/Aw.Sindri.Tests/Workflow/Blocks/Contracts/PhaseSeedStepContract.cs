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

    protected override string ExpectedHash => "sha256:b5605417cc265fa86f6f93042dfb1218434307d27f3c0f7379a549733b0b477e";   // red: the real hash comes with the run

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PhaseSeedStep(new FakeWorktrees(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.phase.seed", []), cancellationToken);
}
