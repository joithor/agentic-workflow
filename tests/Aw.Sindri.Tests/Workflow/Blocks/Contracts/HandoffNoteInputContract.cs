using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class HandoffNoteInputContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrInputs.HandoffNote;

    protected override string ExpectedHash => "sha256:b729b5df3a097a2560e0fd12f9ddef904eea572a68a0ee3fe211819f3b0aec9b";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
