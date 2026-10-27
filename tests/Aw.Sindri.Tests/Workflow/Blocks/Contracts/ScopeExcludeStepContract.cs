using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeExcludeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.Exclude;

    protected override string ExpectedHash => "sha256:27b7f3358a9582f4fcc3350a2ca6b93c106f5b03cc0d2bd1066cf259029378bc";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
