using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeReadTrackerStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.ReadTracker;

    protected override string ExpectedHash => "sha256:1e767ceeda8672f4840d2314115a40d6f140481de25813cf487fc10bf82c1d4b";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
