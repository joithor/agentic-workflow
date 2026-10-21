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

    protected override string ExpectedHash => "sha256:9511ad11643503dca50b9002ad3e1716d6dc2c06f4a67e2e6b70dced24c7c8a8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
