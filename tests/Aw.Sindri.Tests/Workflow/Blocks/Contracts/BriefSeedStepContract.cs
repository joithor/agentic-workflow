using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BriefSeedStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ScopingSteps.BriefSeed;

    protected override string ExpectedHash => "sha256:082179dd9bdf0756ffc2efb58c7ee0ccfbe9c6c259d060af83def5f348070740";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
