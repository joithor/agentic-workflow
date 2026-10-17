using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeReanchorStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.Reanchor;

    protected override string ExpectedHash => "sha256:7449c792e00cf503319435ae0b737bd0efd493eb90a7f8c01880dcdb841acb5f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
