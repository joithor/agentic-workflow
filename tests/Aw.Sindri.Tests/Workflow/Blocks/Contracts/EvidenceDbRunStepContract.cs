using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class EvidenceDbRunStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => EvidenceSteps.DbRun;

    protected override string ExpectedHash => "sha256:2a11047b4e9b0ebba08c94e6f025b4ef5f1b8efe7f785443255db4e237e6b7d8";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
