using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>A block-runtime Step (scoping station, BS-1): it hashes, kinds, registers once, resolves under IBlockStep and honours a cancelled token.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeReadChangeStepContract : BlockContract
{
    protected override Type Block => typeof(ScopeReadChangeStep);

    protected override BlockDescriptor Descriptor => ScopingSteps.ReadChange;

    protected override string ExpectedHash => "sha256:450d6ba90dccd1fc41143ea96d8db5f87aa2b294218a536ff368466a2ee3a94d";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ScopeReadChangeStep(new InMemoryChangeSource(new Dictionary<string, string>(StringComparer.Ordinal), []), new InMemoryRepoCheckouts(), new InMemoryScopingTextShape([]), new PassThroughScrubber(), TimeProvider.System).Run(ScopingStepContexts.Of("step.scope.read-change", []), cancellationToken);
}
