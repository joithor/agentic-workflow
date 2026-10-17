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

    protected override string ExpectedHash => "sha256:7e172d1a423e48ffc9c053c3a5d4ff4edfa5753518e339170a73c685f726b2a6";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
