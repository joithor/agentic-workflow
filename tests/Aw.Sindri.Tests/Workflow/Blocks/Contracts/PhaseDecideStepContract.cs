using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseDecideStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => PhaseSteps.Decide;

    protected override string ExpectedHash => "sha256:8d204141cf946c2e006b76652a28dfe12580828a1bdcfc9dbf6f081845afa3ae";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
