using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BugfixRecordCandidateStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => BugfixSteps.RecordCandidate;

    protected override string ExpectedHash => "sha256:e4624e9608c06bae38a456f5dc271e4469f5e77b03168c5b0c8674c3067c5867";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
