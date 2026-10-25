using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The intake Step of the line's prove station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveIntakeStepContract : BlockContract
{
    protected override Type Block => typeof(ProveIntakeStep);

    protected override BlockDescriptor Descriptor => ProveSteps.Intake;

    protected override string ExpectedHash => "sha256:ec2445e9ecf7e4e1ffc1091eb96246b6623317e1bee53d34d17a1c561b6d37b5";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ProveIntakeStep(new InMemoryBriefFiles(), new InMemoryRepoCheckouts()).Run(BuildShipContexts.Of("step.prove.intake", []), cancellationToken);
}
