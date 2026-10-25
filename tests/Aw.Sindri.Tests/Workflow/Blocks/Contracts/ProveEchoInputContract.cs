using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks.Contracts;

/// <summary>The test-only echo input: it hashes, registers once under the prove gate, resolves as a Step input and honours cancellation.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveEchoInputContract : BlockContract
{
    protected override Type Block => typeof(ProveEchoInput);

    protected override BlockDescriptor Descriptor => ProveEchoInput.Descriptor;

    protected override string ExpectedHash => "sha256:affbeb2d208d80f44e4be87ee47cf872d41d3be50b9f726914471daa1bb67729";

    protected override JsonSerializerOptions Json => ShippedComposition.Json;

    protected override IServiceProvider Compose() => ShippedComposition.Compose(Path.GetTempPath(), proveBlocks: "1");

    protected override async Task InvokeAsync(CancellationToken cancellationToken) =>
        _ = await new ProveEchoInput(new NoEcho()).Fill(
            new StepInputContext(BlockId.From(ProveEchoInput.Id), BlockId.From("step.produce"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>()),
            cancellationToken);

    private sealed class NoEcho : IProveEcho
    {
        public ValueTask<string> ReadText(CancellationToken cancellationToken) => ValueTask.FromResult(string.Empty);

        public ValueTask Log(string line, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
