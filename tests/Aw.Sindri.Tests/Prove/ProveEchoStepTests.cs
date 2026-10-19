using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Tests.Prove;

/// <summary>The test-only block Step 'step.prove.echo' (hand test): it completes 'done' and logs the digests of the sections and typed results it was given, never their text.</summary>
[Lane(TestLane.Unit)]
public sealed class ProveEchoStepTests
{
    private sealed class Echo : IProveEcho
    {
        public List<string> Lines { get; } = [];

        public ValueTask<string> ReadText(CancellationToken cancellationToken) => ValueTask.FromResult(string.Empty);

        public ValueTask Log(string line, CancellationToken cancellationToken)
        {
            Lines.Add(line);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Run_logs_each_section_and_result_digest_without_text_and_completes_done()
    {
        var echo = new Echo();
        var input = BlockId.From("input.prove.echo");
        var context = new StepContext(
            BlockId.From(ProveEchoStep.Id),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<FactKey, JsonElement>(),
            new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { ["echo"] = "PLANT-TEXT" }))
        { InputResults = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { [input.Value] = JsonSerializer.SerializeToElement(new { count = 2 }) } };

        var result = await new ProveEchoStep(echo).Run(context, TestContext.Current.CancellationToken);

        Assert.Equal("done", result.Result!.Outcome.Value);
        Assert.Matches("^step step\\.prove\\.echo echo [0-9a-f]{12}$", echo.Lines[0]);
        Assert.Matches("^step step\\.prove\\.echo result input\\.prove\\.echo [0-9a-f]{12}$", echo.Lines[1]);
        Assert.DoesNotContain(echo.Lines, static l => l.Contains("PLANT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cancelled_token_stops_it()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var context = new StepContext(BlockId.From(ProveEchoStep.Id), new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), InjectionSections.Empty);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await new ProveEchoStep(new Echo()).Run(context, cancelled.Token));
    }
}
