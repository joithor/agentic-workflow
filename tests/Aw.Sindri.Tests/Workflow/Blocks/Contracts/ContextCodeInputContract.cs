using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextCodeInputContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PendingInputs.Code;

    protected override string ExpectedHash => "sha256:5123ce153cfdb6fa6fbfc9b2389f86c50ced60c76c4ca856158159ad6c323094";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
