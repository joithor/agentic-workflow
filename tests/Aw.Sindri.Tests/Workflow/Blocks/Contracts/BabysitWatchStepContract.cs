using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BabysitWatchStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BabysitSteps.Watch;

    protected override string ExpectedHash => "sha256:a3cb1c180259271b02811d78d7a2e2c734386d24349987a6a1a8a3733505b1cd";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
