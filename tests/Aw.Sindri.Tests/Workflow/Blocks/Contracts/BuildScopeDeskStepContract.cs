using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The scope-desk Step of the line's build station, descriptor-only (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildScopeDeskStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BuildSteps.ScopeDesk;

    protected override string ExpectedHash => "sha256:c664df998e5b27415faaeda45f6201e8ca50cd8c4977b3378e6c8f5437ff2311";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
