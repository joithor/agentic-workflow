using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (a human wait, no runtime): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class LensAskStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => LensSteps.Ask;

    protected override string ExpectedHash => "sha256:1d452b677291a0269f83b7bed2655c623758a0097311d15ddfb91e0e782d930f";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
