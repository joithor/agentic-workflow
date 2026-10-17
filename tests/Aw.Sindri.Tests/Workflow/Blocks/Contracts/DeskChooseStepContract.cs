using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class DeskChooseStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ChoiceSteps.DeskChoose;

    protected override string ExpectedHash => "sha256:f01cd85115d1e9d1e12afdb5cdae46a3bdbf7a033d0f95d9973c7847ca6e062c";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
