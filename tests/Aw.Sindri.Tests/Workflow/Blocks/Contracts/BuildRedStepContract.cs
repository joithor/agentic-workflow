using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The red Step of the line's build station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildRedStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BuildSteps.Red;

    protected override string ExpectedHash => "sha256:b56f09a0303f9c1f2251a749b6a37c425cd7d0a72065d38a8478c1549f3fe6e8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
