using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ReproduceTestCheckStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BugfixSteps.ReproduceTestCheck;

    protected override string ExpectedHash => "sha256:53c108eccc335ecce0339b1dd8c390fd7b9acda30c799427cf539e96db7daae8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
