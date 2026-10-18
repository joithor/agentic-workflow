using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class InterviewAnswerStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.InterviewAnswer;

    protected override string ExpectedHash => "sha256:6fdbc0aa65ff51935b3daba0e56081fc082e175b65a65d915fe7bd71bf8f0fbe";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
