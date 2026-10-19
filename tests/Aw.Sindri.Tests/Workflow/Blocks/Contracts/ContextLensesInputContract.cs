using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The real 'input.context.lenses': it hashes (as its descriptor-only form did), registers once, resolves as a Step input and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextLensesInputContract : BlockContract
{
    protected override Type Block => typeof(ContextLensesInput);

    protected override BlockDescriptor Descriptor => ContextLensesInput.Descriptor;

    protected override string ExpectedHash => "sha256:10e07eb604a8092412459861c964ccd631089a1c192fa246826c29b2b82d5f3e";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await ContextLensesInputTests.Input(new ContextLensesInputTests.StubLenses(a => ContextInputFixtures.SmallSet([.. a]))).Fill(
            new StepInputContext(BlockId.From(ContextLensesInput.Id), BlockId.From("step.scope.gather"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>()),
            cancellationToken);
}
