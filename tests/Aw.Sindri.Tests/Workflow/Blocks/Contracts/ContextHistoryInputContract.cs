using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ContextCalls;
using Aw.Sindri.Tests.Workflow.Blocks.Input;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The real 'input.context.history': it hashes (as its descriptor-only form did), registers once, resolves as a Step input and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextHistoryInputContract : BlockContract
{
    protected override Type Block => typeof(ContextHistoryInput);

    protected override BlockDescriptor Descriptor => ContextHistoryInput.Descriptor;

    protected override string ExpectedHash => "sha256:f0530a3e621910fb26ceace373310f46b2b35b46173a72ada286bfe0268392e2";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose();

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await ContextHistoryInputTests.Input(new ContextHistoryInputTests.StubHistory(() => ContextHistoryTextTests.View())).Fill(
            new StepInputContext(BlockId.From(ContextHistoryInput.Id), BlockId.From("step.scope.gather"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>()),
            cancellationToken);
}
