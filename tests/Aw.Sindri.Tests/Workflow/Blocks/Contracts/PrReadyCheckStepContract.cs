using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrReadyCheckStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.ReadyCheck;

    protected override string ExpectedHash => "sha256:f5a609f75a5a5ada53d92a34be422f0075fdafc476dde64119cd8cb3aa37b531";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
