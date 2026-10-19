using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanExecReviewStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PlanSteps.ExecReview;

    protected override string ExpectedHash => "sha256:d1a5de54325eaa16191fc54eefef0cda8a031167d82f6d610681b5efcd4e2ca6";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
