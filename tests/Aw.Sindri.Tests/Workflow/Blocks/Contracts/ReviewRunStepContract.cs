using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A descriptor-only block (no runtime until its phase): it hashes, kinds, registers once and resolves nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewRunStepContract : DescriptorContract
{
    protected override BlockDescriptor Descriptor => ReviewSteps.Run;

    protected override string ExpectedHash => "sha256:a1b6aeacbdf6793b41acecfcfc7f730a626434cde3ca6b3c692d98a019b2f892";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();
}
