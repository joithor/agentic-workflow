using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceDesktopRunStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => EvidenceSteps.DesktopRun;

    protected override string ExpectedHash => "sha256:3c02f1eb936bacd5d72104af6064d05c4f875d875a370c5a571352690ff1f2a0";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
