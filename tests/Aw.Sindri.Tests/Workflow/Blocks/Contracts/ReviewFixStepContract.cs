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

    protected override string ExpectedHash => "sha256:2f7555fa9c8310962a5ffd532a06c14bdb335c2b131296f54108e0aeb3c35aab";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
