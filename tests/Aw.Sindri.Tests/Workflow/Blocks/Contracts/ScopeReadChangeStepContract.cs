using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeReadChangeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.ReadChange;

    protected override string ExpectedHash => "sha256:450d6ba90dccd1fc41143ea96d8db5f87aa2b294218a536ff368466a2ee3a94d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
