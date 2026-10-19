using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The before-after Step of the line's prove station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveBeforeAfterStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ProveSteps.BeforeAfter;

    protected override string ExpectedHash => "sha256:262fd4a9557d27a21e413eeffc1365cf76f1b9c8ea6dec3a6caa3095b01c5ce0";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
