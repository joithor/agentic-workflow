using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The real 'input.context.code': it hashes (as its descriptor-only form did), registers once, resolves as a Step input and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextCodeInputContract : BlockContract
{
    protected override Type Block => typeof(ContextCodeInput);

    protected override BlockDescriptor Descriptor => ContextCodeInput.Descriptor;

    protected override string ExpectedHash => "sha256:5123ce153cfdb6fa6fbfc9b2389f86c50ced60c76c4ca856158159ad6c323094";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await ContextCodeInputTests.Input(new ContextCodeInputTests.StubCode(() => ContextCodeVerbsTests.View())).Fill(
            new StepInputContext(BlockId.From(ContextCodeInput.Id), BlockId.From("step.scope.gather"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>()),
            cancellationToken);
}
