using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.GetJob;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Follows a started job to its end (API spec §3.2, P6 unit H): <c>services run</c> uses it, and so do the context lane's <c>context sources sync</c> and
/// <c>context map build</c>, with the <see cref="JobStartedResponse"/> their job route answered. It prints 'job JOB-… started', opens the links' event
/// stream (client-side filter on the job: the stream carries every job's events), prints each progress line as served (already escaped by the server),
/// then the end: '&lt;outcome&gt;: &lt;summary&gt;[ &lt;code&gt;]' or 'cancelled'. A full stream list (SND-API-012) or an error frame falls back to reading
/// the job every second. The stream ending first is 'not running'. The first Ctrl-C posts CancelJob once and waits up to 5 s for the job to end; the second
/// leaves at once. Exit 0 succeeded, 1 failed, cancelled, left or not running, 2 refused or invalid. With <c>json</c> it prints the JobStarted body, then
/// the final job view, as NDJSON, and no progress lines.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the follower writes.</param>
/// <param name="clock">Times the polling fallback and the wait for the cancel.</param>
internal sealed class JobFollower(ControlPlaneClient client, Output output, TimeProvider clock)
{
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    internal static readonly TimeSpan CancelGrace = TimeSpan.FromSeconds(5);

    /// <summary>Follows <paramref name="started"/> until the job ends.</summary>
    /// <param name="started">What the job route answered.</param>
    /// <param name="json">Print NDJSON (the JobStarted body, then the final JobView) instead of lines.</param>
    /// <param name="interrupt">The first Ctrl-C: cancels the job.</param>
    /// <param name="again">The second Ctrl-C: leaves at once.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> Follow(JobStartedResponse started, bool json, CancellationToken interrupt, CancellationToken again)
    {
        ArgumentNullException.ThrowIfNull(started);
        var run = new JobFollowed(started, json);
        _ = json
            ? await output.Json(started, 0).ConfigureAwait(false)
            : await output.Text($"job {started.JobId.Value} started\n", 0).ConfigureAwait(false);
        using var signals = new JobInterrupts(interrupt, again);
        var ended = await Watch(run, signals).ConfigureAwait(false);
        return ended.IsError ? await output.Error(ended.Error).ConfigureAwait(false) : await Finish(run, ended.Value).ConfigureAwait(false);
    }

    private async Task<Result<JobEnd>> Watch(JobFollowed run, JobInterrupts signals)
    {
        using var end = CancellationTokenSource.CreateLinkedTokenSource(signals.Second);
        await using var frames = client.Stream(run.Started.Links.Events, end.Token).GetAsyncEnumerator(end.Token);
        Task<bool>? read = null;
        try
        {
            return await Loop(frames, run, signals, pending => read = pending).ConfigureAwait(false);
        }
        finally
        {
            // an iterator can't be disposed while a read is outstanding: end the stream, then let the read finish
            await end.CancelAsync().ConfigureAwait(false);
            if (read is not null)
            {
                try
                {
                    _ = await read.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // the read ended with the stream
                }
            }
        }
    }

    private async Task<Result<JobEnd>> Loop(IAsyncEnumerator<Result<StreamFrame>> frames, JobFollowed run, JobInterrupts signals, Action<Task<bool>?> hold)
    {
        Task? grace = null;
        Task<bool>? next = null;
        while (true)
        {
            next ??= frames.MoveNextAsync().AsTask();
            hold(next);
            var winner = await Task.WhenAny(next, signals.SecondSignal, grace ?? signals.FirstSignal).ConfigureAwait(false);
            if (winner == signals.SecondSignal || winner == grace)
            {
                return await Left(run).ConfigureAwait(false);
            }

            if (winner == signals.FirstSignal)
            {
                await PostCancel(run).ConfigureAwait(false);
                grace = Task.Delay(CancelGrace, clock);
                continue;
            }

            next = null;
            hold(null);
            var step = await Step(frames, await ((Task<bool>)winner).ConfigureAwait(false), run, signals).ConfigureAwait(false);
            if (step is null || step.Value.IsError || step.Value.Value is not null)
            {
                return step?.Value ?? SindriErrors.NotRunning.With("not running: the control plane stopped while the job ran");
            }
        }
    }

    /// <summary>Takes the frame the stream gave: null when the stream is over; a result holding null to keep reading; the end otherwise.</summary>
    private async Task<(Result<JobEnd?> Value, bool Open)?> Step(IAsyncEnumerator<Result<StreamFrame>> frames, bool more, JobFollowed run, JobInterrupts signals)
    {
        if (!more)
        {
            return null;
        }

        var frame = frames.Current;
        if (frame.IsError ? frame.Error.Code != SindriErrors.StreamsFull.Code : false)
        {
            return (frame.Error, false);
        }

        if (frame.IsError || frame.Value.Event == "error")
        {
            var polled = await PollInstead(run, signals).ConfigureAwait(false);
            return (polled.IsError ? polled.Error : polled.Value, false);
        }

        var signal = JobFrames.Read(output, frame.Value, run.Started.JobId);
        return (signal is null ? Results.Success<JobEnd?>(value: null) : await Show(run, signal, frame.Value.Id).ConfigureAwait(false), true);
    }

    /// <summary>No stream to follow: read the job every second until it ends. Ctrl-C works as it does on the stream.</summary>
    private async Task<Result<JobEnd>> PollInstead(JobFollowed run, JobInterrupts signals)
    {
        try
        {
            while (true)
            {
                await Task.Delay(Poll, clock, signals.Second).ConfigureAwait(false);
                if (signals.First.IsCancellationRequested && run.CancelledAt is null)
                {
                    await PostCancel(run).ConfigureAwait(false);
                }

                if (run.CancelledAt is { } at && clock.GetUtcNow() - at >= CancelGrace)
                {
                    return await Left(run).ConfigureAwait(false);
                }

                var read = await client.Get<JobView>(run.Started.Links.Job, signals.Second).ConfigureAwait(false);
                if (read.IsError)
                {
                    return read.Error;
                }

                if (await Print(run, read.Value).ConfigureAwait(false) is { } end)
                {
                    return end;
                }
            }
        }
        catch (OperationCanceledException) when (signals.Second.IsCancellationRequested)
        {
            return await Left(run).ConfigureAwait(false);
        }
    }

    /// <summary>Prints the lines of <paramref name="read"/> not printed yet; the end when the job has one.</summary>
    private async Task<JobEnd?> Print(JobFollowed run, ApiAnswer<JobView> read)
    {
        foreach (var line in read.Value.Progress.Where(l => l.Round > run.Printed))
        {
            _ = await Show(run, new JobSignal(line.Round, line.Line, Exit: 0, Ended: false), after: 0).ConfigureAwait(false);
        }

        return JobFrames.Ended(read.Value) is { } ended
            ? (await Show(run, ended, after: 0).ConfigureAwait(false)).Value! with { View = read.Body }
            : null;
    }

    private async Task<Result<JobEnd?>> Show(JobFollowed run, JobSignal signal, long after)
    {
        if (!signal.Ended)
        {
            if (signal.Round > run.Printed && !run.Json)
            {
                _ = await output.Text(signal.Text + "\n", 0).ConfigureAwait(false);
            }

            run.Printed = Math.Max(run.Printed, signal.Round);
            return Results.Success<JobEnd?>(value: null);
        }

        if (!run.Json)
        {
            _ = await output.Text(signal.Text + "\n", 0).ConfigureAwait(false);
        }

        return new JobEnd(signal.Exit, after);
    }

    private async Task PostCancel(JobFollowed run)
    {
        run.CancelledAt = clock.GetUtcNow();
        var posted = await client.Post<CancelJobRequest, CommandAccepted>(run.Started.Links.Cancel, body: null, CancellationToken.None).ConfigureAwait(false);
        if (posted.IsError)
        {
            _ = await output.Error(posted.Error).ConfigureAwait(false);   // the end still arrives as the job's own event
        }
    }

    /// <summary>The follower stops following before the job ended: the Ctrl-C asked to leave, or the wait for the cancel ran out.</summary>
    private async Task<JobEnd> Left(JobFollowed run)
    {
        if (!run.Json)
        {
            _ = await output.Text(run.CancelledAt is null ? "left the job running\n" : "cancel requested; the job may still be running\n", 1).ConfigureAwait(false);
        }

        return new JobEnd(Exit: 1, After: 0, Left: true);
    }

    /// <summary>With --json, the final job view after the JobStarted body.</summary>
    private async Task<int> Finish(JobFollowed run, JobEnd end)
    {
        if (!run.Json || end.Left)
        {
            return end.Exit;
        }

        if (end.View is { } view)
        {
            return await output.Raw(view, end.Exit).ConfigureAwait(false);
        }

        var final = end.After > 0
            ? await client.GetAfter<JobView>(run.Started.Links.Job, end.After, CancellationToken.None).ConfigureAwait(false)
            : await client.Get<JobView>(run.Started.Links.Job, CancellationToken.None).ConfigureAwait(false);
        return final.IsError ? await output.Error(final.Error).ConfigureAwait(false) : await output.Raw(final.Value.Body, end.Exit).ConfigureAwait(false);
    }
}
