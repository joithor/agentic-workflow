using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Lens;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (design lens loop, LN-PK lite): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class LensRoundStepContract : BlockContract
{
    protected override Type Block => typeof(LensRoundStep);

    protected override BlockDescriptor Descriptor => LensRoundSteps.Round;

    protected override string ExpectedHash => "sha256:8e31f7aaa9519e250a145cb37f10aa61bc45746e857e1a7ad7bd0c6358ba8f3b";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new LensRoundStep(new FakeLensPacks()).Run(LensStepContexts.Of("step.lens.round", []), cancellationToken);
}
