using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Evidence;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceCheckProviderContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => EvidenceProviders.Check;

    protected override string ExpectedHash => "sha256:5e8052c5aaf078f6a487a5822fa63d9821a011fa480205fe5bbc8e9f07092d62";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
