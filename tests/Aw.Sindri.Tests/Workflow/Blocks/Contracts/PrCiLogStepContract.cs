using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The ci-log Step of the PR babysitter on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class PrCiLogStepContract : BlockContract
{
    protected override Type Block => typeof(PrCiLogStep);

    protected override BlockDescriptor Descriptor => PrSteps.CiLog;

    protected override string ExpectedHash => "sha256:1d66db3cb0decc1c7ca167076c8a664298eb82cc574300539f4a64e303617786";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new PrCiLogStep(new FakePullRequests(["forge.test"])).Run(BuildShipContexts.Of("step.pr.ci-log", []), cancellationToken);
}
