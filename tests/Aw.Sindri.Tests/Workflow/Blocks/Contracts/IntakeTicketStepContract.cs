using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class IntakeTicketStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BugfixSteps.IntakeTicket;

    protected override string ExpectedHash => "sha256:ea24ee0f0a02b06b48469073d11591f0ed25a9c803c86c8e439cf096e0e2b04f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
