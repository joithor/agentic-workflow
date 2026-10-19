using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanHoldoutStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PlanSteps.Holdout;

    protected override string ExpectedHash => "sha256:6565536e97d9d256dfc1b73de8204fe192b43d9a2cd405b15a606d14ec9e5830";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
