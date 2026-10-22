using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (design station, BS2): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignCheckStepContract : BlockContract
{
    protected override Type Block => typeof(DesignCheckStep);

    protected override BlockDescriptor Descriptor => DesignSteps.Check;

    protected override string ExpectedHash => "sha256:18077dc3b7e862b03855f6a80bbb411a8ba8196fa8ed315ddfb50f5e489d418e";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new DesignCheckStep(new FakeStylePacks()).Run(ScopingStepContexts.Of("step.design.check", []), cancellationToken);
}
