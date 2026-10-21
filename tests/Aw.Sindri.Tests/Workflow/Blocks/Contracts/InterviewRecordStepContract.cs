using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class InterviewRecordStepContract : BlockContract
{
    protected override Type Block => typeof(InterviewRecordStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.InterviewRecord;

    protected override string ExpectedHash => "sha256:fd0e7dfb21b08317f8b98dd809927da7b37fd9029588e5cf3780dcb773ec7e6e";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new InterviewRecordStep().Run(ScopingStepContexts.Of("step.interview.record", []), cancellationToken);
}
