using System.Globalization;
using Aw.Sindri.Core.Control;
using Microsoft.Extensions.Logging;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="ITemporalWorker"/> over the SDK's TemporalWorker (engine spec rev 8 §0 "Operations"): the worker 'sindri
/// serve' runs on task queue 'sindri' of the dev server it keeps. Engine P4a: a frontend that doesn't answer is
/// logged and serve runs on without a worker (P4 makes it a health failure).
/// </summary>
/// <param name="steps">The RunStep activities.</param>
/// <param name="events">The RecordEvent activity.</param>
/// <param name="items">The ReadItemRepo activity.</param>
/// <param name="logger">The log.</param>
internal sealed partial class SindriWorkerHost(RunStepActivities steps, RecordEventActivities events, ItemFactActivities items, ILogger<SindriWorkerHost> logger) : ITemporalWorker
{
    public async Task Run(TemporalEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var target = string.Create(CultureInfo.InvariantCulture, $"127.0.0.1:{endpoint.Port}");
        ITemporalClient client;
        try
        {
            client = await TemporalClient.ConnectAsync(new TemporalClientConnectOptions(target) { Namespace = SindriTemporal.Namespace }).ConfigureAwait(false);
        }
        catch (Exception e) when (e is RpcException or InvalidOperationException) // boundary: serve runs on; the doctor's temporal line shows why
        {
            NoWorker(logger, target, e.Message);
            return;
        }

        using var worker = new TemporalWorker(client, SindriWorker.Options(steps, events, items));
        Polling(logger, target, SindriTemporal.TaskQueue);
        try
        {
            await worker.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // serve is stopping
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "worker polls task queue {Queue} on {Target}")]
    private static partial void Polling(ILogger logger, string target, string queue);

    [LoggerMessage(Level = LogLevel.Error, Message = "no worker: Temporal at {Target} doesn't answer: {Problem}")]
    private static partial void NoWorker(ILogger logger, string target, string problem);
}
