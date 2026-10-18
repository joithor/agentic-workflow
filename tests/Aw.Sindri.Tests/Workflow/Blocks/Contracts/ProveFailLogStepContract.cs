using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The fail-log Step of the line's prove station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveFailLogStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ProveSteps.FailLog;

    protected override string ExpectedHash => "sha256:e8a23a1cf1890c120978d51ae3f160bbcd77e1e2852897df49171deb4941632f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
