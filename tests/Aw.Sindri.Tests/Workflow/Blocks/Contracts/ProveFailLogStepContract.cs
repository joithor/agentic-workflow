using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The faillog Step of the line's prove station on the block runtime: it hashes, kinds, registers once, resolves under its kind and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveFailLogStepContract : BlockContract
{
    protected override Type Block => typeof(ProveFailLogStep);

    protected override BlockDescriptor Descriptor => ProveSteps.FailLog;

    protected override string ExpectedHash => "sha256:e8a23a1cf1890c120978d51ae3f160bbcd77e1e2852897df49171deb4941632f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ProveFailLogStep().Run(BuildShipContexts.Of("step.prove.fail-log", []), cancellationToken);
}
