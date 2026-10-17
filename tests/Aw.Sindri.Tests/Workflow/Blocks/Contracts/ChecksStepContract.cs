using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ChecksStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => DeskSteps.Checks;

    protected override string ExpectedHash => "sha256:8d6a4302bc9aa7ab780620c014cbc2954c3b9e4cef153be63bb1a2eeb72c2fe8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
