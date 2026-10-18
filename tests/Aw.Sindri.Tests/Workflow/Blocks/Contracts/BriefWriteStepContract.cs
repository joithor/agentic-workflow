using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BriefWriteStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.BriefWrite;

    protected override string ExpectedHash => "sha256:74ab2c64882b66558c7b6595286cf5adf4f4e8e81cac125eb35f97ad9bafdca2";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
