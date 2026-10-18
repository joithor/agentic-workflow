using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The green Step of the line's build station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildGreenStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BuildSteps.Green;

    protected override string ExpectedHash => "sha256:c2960a6d337fb70bdc75485818e9f0b3192fb2f2f6d316f8785283c6c0411000";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
