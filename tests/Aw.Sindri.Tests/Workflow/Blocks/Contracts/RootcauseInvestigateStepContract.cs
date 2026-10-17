using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class RootcauseInvestigateStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BugfixSteps.RootcauseInvestigate;

    protected override string ExpectedHash => "sha256:089db1c623c8ac46b368e17c20432e4384db243721ce09c7848cd940c23cd6d0";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
