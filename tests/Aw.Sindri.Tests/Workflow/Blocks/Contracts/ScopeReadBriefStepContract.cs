using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeReadBriefStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.ReadBrief;

    protected override string ExpectedHash => "sha256:34ed392d91e7189a9219fd6f9f586f716dc76368ef52f3b0192440a7dcf3e200";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
