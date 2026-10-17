using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class JudgeChooseStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ChoiceSteps.JudgeChoose;

    protected override string ExpectedHash => "sha256:82a9048fab96609537956ae77edd785aa29848ae083c762b8de7703c0e7bbfc3";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
