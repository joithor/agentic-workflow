using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class HandtestRunStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ProofSteps.HandtestRun;

    protected override string ExpectedHash => "sha256:3ec327e0820b5cb0b02b62c6ee95b93f74f626a800f095d751811aff55bd4a49";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
