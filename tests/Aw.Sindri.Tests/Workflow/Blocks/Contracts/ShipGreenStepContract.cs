using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Ship;
using Aw.Sindri.Testing.Checks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The green Step of the ship station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipGreenStepContract : BlockContract
{
    protected override Type Block => typeof(ShipGreenStep);

    protected override BlockDescriptor Descriptor => ShipSteps.Green;

    protected override string ExpectedHash => "sha256:6d6ae80950830824f1e7a500434ccec341b186212f13f61dc75882cf458a9831";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken)
    {
        var rig = new Workflow.BuildShip.StepRig();
        _ = await new ShipGreenStep(rig.Diff, rig.Changes, rig.Files, rig.Runner, new IdentityScrubber()).Run(BuildShipContexts.Of("step.ship.green", []), cancellationToken);
    }
}
