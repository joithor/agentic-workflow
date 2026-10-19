using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The holdout-log Step of the line's prove station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveHoldoutLogStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ProveSteps.HoldoutLog;

    protected override string ExpectedHash => "sha256:deb2e71a9314e89c5194d38a1594e2de96a54ac872ed18dff61d07477967e867";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
