using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrReworkNotesStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrSteps.ReworkNotes;

    protected override string ExpectedHash => "sha256:c8a767f14b24b8f3533acf04acfd851252b3b32d42e83b2ce5cb0b645a8e09df";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
