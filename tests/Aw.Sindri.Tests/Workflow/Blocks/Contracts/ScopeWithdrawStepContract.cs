using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeWithdrawStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.Withdraw;

    protected override string ExpectedHash => "sha256:f4ee57b7ed70b510bc06e7a84594387159a8f69edb8e4da4a660aca30e991749";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
