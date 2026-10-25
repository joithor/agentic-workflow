using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.GetJob;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// Follows a started job to its end (API spec §3.2, P6 unit H): 'services run' uses it, and so do the context lane's 'context sources sync' and
/// 'context map build', with the <see cref="JobStartedResponse"/> their job route answered. It prints 'job JOB-… started', opens the links' event
/// stream (client-side filter on the job: the stream carries every job's events), prints each progress line as served (already escaped by the server),
/// then the end: '&lt;outcome&gt;: &lt;summary&gt;[ &lt;code&gt;]' or 'cancelled'. A full stream list (SND-API-012) or an error frame falls back to reading
/// the job every second. The stream ending first is 'not running'. The first Ctrl-C posts CancelJob once and waits up to 5 s for the job to end; the second
/// leaves at once. Exit 0 succeeded, 1 failed, cancelled, left or not running, 2 refused or invalid. With json it prints the JobStarted body, then
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
    /// <param name="describe">Words to print after the end line of a job that succeeded (a table of its result, say), read from the final job; null for none. Not used with json.</param>
    /// <returns>The exit code.</returns>
    internal async Task<int> Follow(JobStartedResponse started, bool json, CancellationToken interrupt, CancellationToken again, Func<JobView, string?>? describe = null)
    {
        ArgumentNullException.ThrowIfNull(started);
        var run = new JobFollowed(started, json);
        _ = json
            ? await output.Json(started, 0).ConfigureAwait(false)
            : await output.Text($"job {started.JobId.Value} started\n", 0).ConfigureAwait(false);
        using var signals = new JobInterrupts(interrupt, again);
        var ended = await Watch(run, signals).ConfigureAwait(false);
        return ended.IsError ? await output.Error(ended.Error).ConfigureAwait(false) : await Finish(run, ended.Value, describe, again).ConfigureAwait(false);
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
                if (!await PostCancel(run, signals.Second).ConfigureAwait(false))
                {
                    return await Left(run).ConfigureAwait(false);   // the second Ctrl-C came while the request was in flight
                }

                grace = Task.Delay(CancelGrace, clock, signals.Second);
                continue;
            }

            next = null;
            hold(null);
            if (await Frame((Task<bool>)winner, frames, run, signals).ConfigureAwait(false) is { } result)
            {
                return result;
            }
        }
    }

    /// <summary>The next frame, or the stream's end: null to keep reading, otherwise how following ended.</summary>
    private async Task<Result<JobEnd>?> Frame(Task<bool> read, IAsyncEnumerator<Result<StreamFrame>> frames, JobFollowed run, JobInterrupts signals)
    {
        bool more;
        try
        {
            more = await read.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (signals.Second.IsCancellationRequested)
        {
            return await Left(run).ConfigureAwait(false);   // the stream was cancelled by the second Ctrl-C before its signal was seen
        }

        return more ? await Handle(frames.Current, run, signals).ConfigureAwait(false) : SindriErrors.NotRunning.With("not running: the control plane stopped while the job ran");
    }

    /// <summary>Takes the frame the stream gave: null to keep reading, otherwise how following ended.</summary>
    private async Task<Result<JobEnd>?> Handle(Result<StreamFrame> frame, JobFollowed run, JobInterrupts signals)
    {
        if (frame.IsError && frame.Error.Code != SindriErrors.StreamsFull.Code)
        {
            return frame.Error;
        }

        if (frame.IsError || frame.Value.Event == "error")
        {
            return await PollInstead(run, signals).ConfigureAwait(false);
        }

        if (JobFrames.Read(output, frame.Value, run.Started.JobId) is not { } signal)
        {
            return null;
        }

        var end = await Show(run, signal, frame.Value.Id).ConfigureAwait(false);
        return end is null ? null : Kernel.Results.Success(end);
    }

    /// <summary>No stream to follow: read the job every second until it ends. Ctrl-C works as it does on the stream.</summary>
    private async Task<Result<JobEnd>> PollInstead(JobFollowed run, JobInterrupts signals)
    {
        try
        {
            while (true)
            {
                await Task.Delay(Poll, clock, signals.Second).ConfigureAwait(false);
                if (signals.First.IsCancellationRequested && run.CancelledAt is null && !await PostCancel(run, signals.Second).ConfigureAwait(false))
                {
                    return await Left(run).ConfigureAwait(false);
                }

                if (run.CancelledAt is { } at && clock.GetUtcNow() - at >= CancelGrace)
                {
                    return await Left(run).ConfigureAwait(false);
                }

                if (await ReadOnce(run, signals).ConfigureAwait(false) is { } done)
                {
                    return done;
                }
            }
        }
        catch (OperationCanceledException) when (signals.Second.IsCancellationRequested)
        {
            return await Left(run).ConfigureAwait(false);
        }
    }

    /// <summary>One read of the job at the start's sequence (so a view that has not folded 'job.started' yet waits instead of answering 404): the end or the error that ends following, null to read again.</summary>
    private async Task<Result<JobEnd>?> ReadOnce(JobFollowed run, JobInterrupts signals)
    {
        var read = await client.GetAfter<JobView>(run.Started.Links.Job, run.Started.Accepted.Sequence, signals.Second).ConfigureAwait(false);
        if (read.IsError)
        {
            if (Transient(read.Error))
            {
                return null;
            }

            return read.Error;
        }

        return await Print(run, read.Value).ConfigureAwait(false) is { } end ? Kernel.Results.Success(end) : null;
    }

    /// <summary>Prints the lines of <paramref name="read"/> not printed yet; the end when the job has one.</summary>
    private async Task<JobEnd?> Print(JobFollowed run, ApiAnswer<JobView> read)
    {
        foreach (var line in read.Value.Progress.Where(l => l.Round > run.Printed))
        {
            _ = await Show(run, new JobSignal(line.Round, line.Line, Exit: 0, Ended: false), after: 0).ConfigureAwait(false);
        }

        return JobFrames.Ended(read.Value) is { } ended && await Show(run, ended, after: 0).ConfigureAwait(false) is { } end
            ? end with { View = read.Body }
            : null;
    }

    /// <summary>Prints a progress line or the end; the end as a result, null for a line.</summary>
    private async Task<JobEnd?> Show(JobFollowed run, JobSignal signal, long after)
    {
        if (!run.Json && (signal.Ended || signal.Round > run.Printed))
        {
            _ = await output.Text(signal.Text + "\n", 0).ConfigureAwait(false);
        }

        run.Printed = Math.Max(run.Printed, signal.Round);
        return signal.Ended ? new JobEnd(signal.Exit, after) : null;
    }

    /// <summary>A read the next poll can answer: the views hadn't reached the start yet, or the control plane was slow.</summary>
    private static bool Transient(Error error) => error.Code == SindriErrors.ViewsBehind.Code || error.Code == SindriErrors.ApiTimeout.Code;

    /// <summary>Asks the job to cancel; false when the second Ctrl-C came while the request was in flight (the request is dropped, the follower leaves).</summary>
    private async Task<bool> PostCancel(JobFollowed run, CancellationToken second)
    {
        run.CancelledAt = clock.GetUtcNow();
        try
        {
            var posted = await client.Post<CancelJobRequest, CommandAccepted>(run.Started.Links.Cancel, body: null, second).ConfigureAwait(false);
            if (posted.IsError)
            {
                _ = await output.Error(posted.Error).ConfigureAwait(false);   // the end still arrives as the job's own event
            }

            return true;
        }
        catch (OperationCanceledException) when (second.IsCancellationRequested)
        {
            return false;
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

    /// <summary>With --json, the final job view after the JobStarted body; otherwise, for a job that succeeded, what <paramref name="describe"/> says about it.</summary>
    private async Task<int> Finish(JobFollowed run, JobEnd end, Func<JobView, string?>? describe, CancellationToken leave)
    {
        var wanted = run.Json || (describe is not null && end.Exit == 0);
        if (end.Left || !wanted)
        {
            return end.Exit;
        }

        if (run.Json && end.View is { } view)
        {
            return await output.Raw(view, end.Exit).ConfigureAwait(false);
        }

        var final = await FinalView(run, end, leave).ConfigureAwait(false);
        if (final.IsError)
        {
            return await output.Error(final.Error).ConfigureAwait(false);
        }

        return run.Json
            ? await output.Raw(final.Value.Body, end.Exit).ConfigureAwait(false)
            : await output.Text(describe!(final.Value.Value) ?? string.Empty, end.Exit).ConfigureAwait(false);   // why: 'wanted' without json means describe is set
    }

    private async Task<Result<ApiAnswer<JobView>>> FinalView(JobFollowed run, JobEnd end, CancellationToken leave) => end.After > 0
        ? await client.GetAfter<JobView>(run.Started.Links.Job, end.After, leave).ConfigureAwait(false)
        : await client.Get<JobView>(run.Started.Links.Job, leave).ConfigureAwait(false);
}
