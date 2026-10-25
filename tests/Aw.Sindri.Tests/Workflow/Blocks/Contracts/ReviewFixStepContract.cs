using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewFixStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ReviewSteps.Fix;

    protected override string ExpectedHash => "sha256:ad46d710894d7130254532f20eff470fe1673ea0359519d41671f0522196aae8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
