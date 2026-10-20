using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class RecheckLogStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => RecheckSteps.Log;

    protected override string ExpectedHash => "sha256:f3598a3be81c94e1a07d1e309721d66e8831f60f824bab462ec06ee9560d86e8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
