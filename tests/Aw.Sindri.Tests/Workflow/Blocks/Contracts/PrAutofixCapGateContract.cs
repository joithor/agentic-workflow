using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PrAutofixCapGateContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PrGates.AutofixCap;

    protected override string ExpectedHash => "sha256:2b9014eec74d63507b4de158e4bbde35aaedc6e6f1858594ddc07bbe41c19c84";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
