using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BugfixJudgeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BugfixSteps.Judge;

    protected override string ExpectedHash => "sha256:b85ef223337f347fa5644a3c2739dbd330b13895ffe1b919a10f779ceb65e386";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
