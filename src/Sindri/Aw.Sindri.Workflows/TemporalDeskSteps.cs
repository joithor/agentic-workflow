using System.Security.Cryptography;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IDeskSteps"/> over Temporal's async activity completion (engine spec rev 8 §5.3). Engine P0 spike: the CLI
/// talks to Temporal directly (no 'serve' route yet), at the frontend control.json records unless '--temporal' names
/// one: it stores each output file under the home, finds the run's task token in <see cref="TaskTokens"/>, completes
/// the RunStep activity with the report, and waits on the 'GetInstance' Query until the instance has left the run.
/// (Engine spec rev 8.1 §5.3: P4 replaces the async completion with the 'complete' Update.)
/// </summary>
/// <param name="home">Where the facts go.</param>
/// <param name="tokens">The run-token-to-task-token map.</param>
/// <param name="frontend">Finds the dev server.</param>
internal sealed class TemporalDeskSteps(WorkflowsHome home, TaskTokens tokens, TemporalFrontend frontend) : IDeskSteps
{
    private const int MaxOutputBytes = 1024 * 1024;
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    public async ValueTask<Result<DeskOutcome>> Complete(DeskReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        var address = report.Address.Value;
        var client = await frontend.Connect(report.Temporal, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        if (tokens.Find(address, report.Run.Value) is not { } taskToken)
        {
            return WorkflowErrors.StaleRunToken.With(
                $"{address} has no open desk run {report.Run.Value} (fix: sindri workflow status {address} shows the open run)");
        }

        var outputs = await StoreAll(report, cancellationToken).ConfigureAwait(false);
        if (outputs.IsError)
        {
            return outputs.Error;
        }

        try
        {
            await client.Value.GetAsyncActivityHandle(taskToken).CompleteAsync(new StepReport(report.Outcome.Value, outputs.Value)).ConfigureAwait(false);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound)
        {
            tokens.Remove(address, report.Run.Value);
            return WorkflowErrors.StaleRunToken.With($"run {report.Run.Value} of {address} is no longer open: {e.Message}");
        }

        tokens.Remove(address, report.Run.Value);
        return report.Wait
            ? await Next(client.Value, address, report.Run.Value, cancellationToken).ConfigureAwait(false)
            : new DeskOutcome(State: null, Run: null);
    }

    /// <summary>Waits until the instance has left the run: the 'GetInstance' Query shows another run token or an end.</summary>
    private static async Task<Result<DeskOutcome>> Next(ITemporalClient client, string address, string run, CancellationToken cancellationToken)
    {
        var handle = client.GetWorkflowHandle<RecipeWorkflow>(address);
        var deadline = DateTimeOffset.UtcNow + _patience;
        InstanceSnapshot? last = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            last = await handle.QueryAsync(static wf => wf.GetInstance()).ConfigureAwait(false);
            if (last.Run != run || last.Outcome is not null)
            {
                return new DeskOutcome(last.State, last.Run is { } open ? RunToken.From(open) : null);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }

        return new DeskOutcome(last?.State, Run: null);
    }

    private async Task<Result<IReadOnlyDictionary<string, FactValue>>> StoreAll(DeskReport report, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        foreach (var (fact, file) in report.Outputs)
        {
            var stored = await Store(file, cancellationToken).ConfigureAwait(false);
            if (stored.IsError)
            {
                return stored.Error;
            }

            outputs[fact.Value] = stored.Value;
        }

        return outputs;
    }

    /// <summary>Stores an output file's content 0600 under the home by its SHA-256, before the completion that references it.</summary>
    private async Task<Result<FactValue>> Store(string file, CancellationToken cancellationToken)
    {
        if (!File.Exists(file) || new FileInfo(file).Length > MaxOutputBytes)
        {
            return WorkflowErrors.MalformedReport.With($"output file {file} is missing or larger than 1 MiB");
        }

        var content = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
        var sha = Convert.ToHexStringLower(SHA256.HashData(content));
        _ = Directory.CreateDirectory(home.FactsDirectory);
        var stored = Path.Combine(home.FactsDirectory, sha);
        await File.WriteAllBytesAsync(stored, content, cancellationToken).ConfigureAwait(false);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(stored, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return new FactValue("fact:sha256:" + sha, sha, HeadSha: null);
    }
}
