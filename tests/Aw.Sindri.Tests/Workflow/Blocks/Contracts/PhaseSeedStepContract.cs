using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseSeedStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PhaseSteps.Seed;

    protected override string ExpectedHash => "sha256:94b70147dc6b9d3da9fe931ea8d97abb81b1e0833238fe889481eb209e51b4cf";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
