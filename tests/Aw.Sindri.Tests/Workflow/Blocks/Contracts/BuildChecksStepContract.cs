using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Build;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.BuildShip;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The checks Step of the line's build station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildChecksStepContract : BlockContract
{
    protected override Type Block => typeof(BuildChecksStep);

    protected override BlockDescriptor Descriptor => BuildSteps.Checks;

    protected override string ExpectedHash => "sha256:e9bdefc6a042cf6266a4589b8e0c548450ab9631551bb5a41e4ee92d43f65877";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var rig = new StepRig();
        _ = await new BuildChecksStep(rig.Diff, rig.Changes, rig.Files, rig.Runner, new IdentityScrubber()).Run(BuildShipContexts.Of("step.build.checks", []), cancellationToken);
    }
}
