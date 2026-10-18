using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ReportWriteStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BugfixSteps.ReportWrite;

    protected override string ExpectedHash => "sha256:d7b9f03e2b946166e8d958866b8d9543e16286bcf21d0f31e0815d29918c40c1";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
