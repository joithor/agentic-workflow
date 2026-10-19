using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipPackStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ShipSteps.Pack;

    protected override string ExpectedHash => "sha256:23d1f6fdb7fadca6c5cae974fd3d03a76046f2d1e73a876bf630c8f1b5f8982e";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
