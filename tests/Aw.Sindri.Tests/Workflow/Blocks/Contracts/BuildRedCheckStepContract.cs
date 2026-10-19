using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The red-check Step of the line's build station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildRedCheckStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BuildSteps.RedCheck;

    protected override string ExpectedHash => "sha256:cb11760a81dece5fc73d0440e5380ec9d8a6c894a482f319b852b627dd1fd6fe";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
