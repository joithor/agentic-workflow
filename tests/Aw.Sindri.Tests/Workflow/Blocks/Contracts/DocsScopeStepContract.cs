using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class DocsScopeStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => DocsSteps.Scope;

    protected override string ExpectedHash => "sha256:bc6d180984e8dce30ae22e53d7d83d9a8f183086686602f106929726dd1e0ff6";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
