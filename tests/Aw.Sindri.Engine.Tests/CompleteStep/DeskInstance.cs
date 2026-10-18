using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>A 'workflow.step' instance on an item, run by the worker 'sindri serve' runs, and the 'workflow' verbs run in process against it.</summary>
/// <param name="Sindri">The composed host over the test's home.</param>
/// <param name="Handle">The instance.</param>
internal sealed record DeskInstance(IServiceProvider Sindri, WorkflowHandle<RecipeWorkflow, RecipeResult> Handle)
{
    /// <summary>Runs 'sindri workflow complete &lt;address&gt; &lt;args&gt;'.</summary>
    public async Task<CliRun> Complete(params string[] args)
    {
        var (exit, stdout, stderr) = await WorkflowsHarness.Cli(Sindri, ["complete", Handle.Id, .. args]);
        return new CliRun(exit, stdout, stderr);
    }

    /// <summary>The instance as 'GetInstance' shows it once <paramref name="holds"/>.</summary>
    public Task<InstanceView> Until(Func<InstanceView, bool> holds) => WorkflowsHarness.Until(Handle, holds);

    /// <summary>The 'workflow.*' ledger events, in order.</summary>
    public async Task<IReadOnlyList<string>> Events()
    {
        var read = await Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, TestContext.Current.CancellationToken);
        Assert.False(read.IsError, read.IsError ? read.Error.Message : null);
        return [.. read.Value.Where(static e => e.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal)).Select(static e => e.Envelope.Type)];
    }

    /// <summary>The payload of each 'workflow.*' ledger event of <paramref name="type"/>, in order.</summary>
    public async Task<IReadOnlyList<System.Text.Json.JsonElement>> Payloads(string type)
    {
        var read = await Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, TestContext.Current.CancellationToken);
        Assert.False(read.IsError, read.IsError ? read.Error.Message : null);
        return [.. read.Value.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal)).Select(static e => System.Text.Json.JsonDocument.Parse(e.Envelope.Payload).RootElement.Clone())];
    }
}
