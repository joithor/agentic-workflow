using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.JobProbe;
using Aw.Sindri.Features.RunService;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Commands;

namespace Aw.Sindri.Tests.JobProbe;

/// <summary>The opt-in 'job-probe' diagnostic service (API P6 unit I): 250 lines, one every 250 ms, with an OSC-8 link, a credential-shaped fake and a long line in them.</summary>
public sealed class JobProbeTests
{
    private static readonly ScheduledRun _run = new(new LeaseGrant(FakeProcessProbe.Holder(100), 1, KnownInstants.Epoch), KnownInstants.Epoch);
    private readonly FollowRig.CountingClock _clock = new();
    private readonly Lines _lines = new();

    private (FakeJobContextAccessor Jobs, JobProbeService Probe) Probe()
    {
        var jobs = new FakeJobContextAccessor();
        return (jobs, new JobProbeService(jobs, _clock));
    }

    private async Task<(Task<Kernel.Result<ScheduledResult>> Done, IDisposable Entered)> Start(CancellationToken ct)
    {
        var (jobs, probe) = Probe();
        var entered = jobs.Enter(new JobContext(JobId.From("JOB-0123456789abcdef0123456789abcdef"), JobKind.From("RunService"), _lines));
        var done = Task.Run(async () => await probe.Run(_run, ct), CancellationToken.None);
        await _lines.Reached(1);   // the first line is written, then the probe sets its timer
        return (done, entered);
    }

    /// <summary>Moves the clock one pause at a time, each time the probe is waiting on a new timer, until it ends; <paramref name="from"/> timers have been set already.</summary>
    private async Task Pump(Task done, int from)
    {
        for (var timers = from; timers < JobProbeService.Lines; timers++)
        {
            await _clock.TimersReached(timers);
            _clock.Advance(JobProbeService.Pause);
        }

        await done;
    }

    private async Task<IReadOnlyList<string>> RunToEnd()
    {
        var (done, entered) = await Start(CancellationToken.None);
        using (entered)
        {
            await Pump(done, from: 1);

            Assert.Equal("ok: 250 lines", (await done).Value.Summary);
        }

        return _lines.All;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task It_writes_250_lines_one_every_250_ms_and_ends_ok_250_lines()
    {
        var (done, entered) = await Start(CancellationToken.None);
        using (entered)
        {
            await _clock.TimersReached(1);
            _ = Assert.Single(_lines.All);   // the first line is written at once, then it waits
            _clock.Advance(TimeSpan.FromMilliseconds(249));
            _ = Assert.Single(_lines.All);
            _clock.Advance(TimeSpan.FromMilliseconds(1));
            await _lines.Reached(2);
            Assert.Equal(2, _lines.All.Count);
            await Pump(done, from: 2);

            Assert.Equal(250, _lines.All.Count);
            Assert.Equal(new ScheduledResult(Idle: false, "ok: 250 lines"), (await done).Value);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cancel_stops_it_within_one_line()
    {
        using var cancel = new CancellationTokenSource();
        var (done, entered) = await Start(cancel.Token);
        using (entered)
        {
            await _clock.TimersReached(1);
            _clock.Advance(TimeSpan.FromMilliseconds(250));
            await _clock.TimersReached(2);   // line 2 is written, then the probe sets its next timer
            var written = _lines.All.Count;

            await cancel.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await done);

            Assert.InRange(_lines.All.Count, written, written + 1);
            Assert.True(_lines.All.Count < 10);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Line_5_carries_an_osc_8_link_that_arrives_escaped()
    {
        var lines = await RunToEnd();

        Assert.Contains("\u001b]8;;", lines[4], StringComparison.Ordinal);
        var cleaned = JobLines.Clean(new RedactingScrubber(Redactor.Default), lines[4]);
        Assert.DoesNotContain('\u001b', cleaned);
        Assert.Contains("\\x1B", cleaned, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Line_6_carries_a_credential_shaped_fake_that_arrives_scrubbed()
    {
        var lines = await RunToEnd();
        var scrubber = new RedactingScrubber(Redactor.Default);

        var cleaned = JobLines.Clean(scrubber, lines[5]);

        Assert.False(string.Equals(lines[5], scrubber.Scrub(lines[5]), StringComparison.Ordinal));
        Assert.DoesNotContain("ghp_", cleaned, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Line_7_is_300_characters_and_is_cut_to_200()
    {
        var lines = await RunToEnd();

        Assert.Equal(300, lines[6].Length);
        Assert.Equal(JobLimits.LineChars, JobLines.Clean(new RedactingScrubber(Redactor.Default), lines[6]).Length);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_200_lines_are_kept()
    {
        var lines = await RunToEnd();
        IReadOnlyList<JobLine> kept = [];

        for (var n = 0; n < lines.Count; n++)
        {
            kept = JobLines.Keep(kept, new JobLine(n + 1, lines[n], KnownInstants.Epoch));
        }

        Assert.Equal((200, 51, 250), (kept.Count, kept[0].Round, kept[^1].Round));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, "ok: worked")]
    [InlineData(true, "ok: idle")]
    public void A_service_without_a_summary_keeps_the_default(bool idle, string expected) => Assert.Equal(expected, RunServiceSummary.Of(new ScheduledResult(idle)));

    [Fact, Trait("Lane", "unit")]
    public void A_service_with_a_summary_reports_it() => Assert.Equal("ok: 250 lines", RunServiceSummary.Of(new ScheduledResult(Idle: false, "ok: 250 lines")));

    private sealed class Lines : IProgress<string>
    {
        private readonly List<string> _all = [];
        private readonly List<(int Count, TaskCompletionSource Reached)> _waiting = [];

        public IReadOnlyList<string> All
        {
            get
            {
                lock (_all)
                {
                    return [.. _all];
                }
            }
        }

        /// <summary>A task that completes once <paramref name="count"/> lines were reported.</summary>
        /// <param name="count">How many.</param>
        /// <returns>The task.</returns>
        public Task Reached(int count)
        {
            lock (_all)
            {
                if (_all.Count >= count)
                {
                    return Task.CompletedTask;
                }

                var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiting.Add((count, reached));
                return reached.Task;
            }
        }

        public void Report(string value)
        {
            lock (_all)
            {
                _all.Add(value);
                foreach (var (count, reached) in _waiting.Where(w => w.Count <= _all.Count).ToList())
                {
                    _ = reached.TrySetResult();
                    _ = _waiting.Remove((count, reached));
                }
            }
        }
    }
}
