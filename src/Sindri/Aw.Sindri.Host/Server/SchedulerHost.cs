using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Runs the background services the approved profile names, and the build's built-in services (engine spec §7.2 S1-D8),
/// on their cadence (factory addendum §17.29.1). It re-reads the approval once a second, so an approval takes effect
/// without a restart; a failed run is logged and the next slot still runs, and an approval it can't read is logged once
/// (SND-SCHED-001) while the current schedule keeps running. Each service runs on its own loop (S1-D10): a run still in
/// flight holds back no other service, and its own slots that come due meanwhile are skipped, never overlapped. Nothing
/// runs while the process holds no lease; a stop waits for the runs in flight, which see the stop's cancellation.
/// </summary>
internal sealed partial class SchedulerHost(IServiceScopeFactory scopes, IApprovals approvals, HeldLease held, IEnumerable<BuiltInService> builtIns, TimeProvider clock, ILogger<SchedulerHost> log)
    : BackgroundService
{
    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);
    private readonly Dictionary<ServiceName, Task> _running = [];
    private IReadOnlyList<ServiceSchedule> _schedules = [];
    private ProfileHash? _profile;
    private bool _loaded;
    private string? _problem;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Refresh(stoppingToken).ConfigureAwait(false);
            var grant = held.Current;
            if (grant is not null)
            {
                await RunDue(grant, stoppingToken).ConfigureAwait(false);
            }

            var next = grant is null || _schedules.Count == 0 ? _tick : _schedules.Min(s => s.NextSlot) - clock.GetUtcNow();
            await Delay(next < TimeSpan.Zero ? TimeSpan.Zero : next > _tick ? _tick : next, stoppingToken).ConfigureAwait(false);
        }

        await InFlight().ConfigureAwait(false);
    }

    /// <summary>Waits for the runs still in flight; they see the stop's cancellation.</summary>
    private async Task InFlight()
    {
        try
        {
            await Task.WhenAll(_running.Values).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // a run the stop cancelled
        }
    }

    private async Task Refresh(CancellationToken ct)
    {
        Result<LoadedProfile?> approved;
        try
        {
            approved = await approvals.Latest(ct).ConfigureAwait(false);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: a broken read (its own timeout too) must not stop the schedule
        {
            approved = SindriErrors.ApprovalsUnavailable.With(e.Message);
        }

        if (approved.IsError)
        {
            if (!string.Equals(_problem, approved.Error.Message, StringComparison.Ordinal))
            {
                _problem = approved.Error.Message;
                LogRefreshFailed(log, approved.Error.Code, approved.Error.Message);
            }

            return;
        }

        _problem = null;
        var profile = approved.Value;
        if (!_loaded || profile?.Hash != _profile)
        {
            _loaded = true;   // the first read loads the built-ins even with no approved profile (S1-D8)
            _profile = profile?.Hash;
            _schedules = ServiceSchedule.From(profile?.Profile, builtIns, clock.GetUtcNow());
            LogSchedule(log, _profile?.Abbreviated ?? "none", _schedules.Count);
        }
    }

    private async Task RunDue(LeaseGrant grant, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var advanced = new List<ServiceSchedule>(_schedules.Count);
        foreach (var schedule in _schedules)
        {
            if (schedule.NextSlot > now)
            {
                advanced.Add(schedule);
                continue;
            }

            if (!_running.TryGetValue(schedule.Name, out var inFlight) || inFlight.IsCompleted)
            {
                var run = RunOne(schedule, new ScheduledRun(grant, schedule.NextSlot), ct);   // its own loop: not awaited here (S1-D10)
                if (run.IsCompleted)
                {
                    await run.ConfigureAwait(false);
                    _ = _running.Remove(schedule.Name);
                }
                else
                {
                    _running[schedule.Name] = run;
                }
            }

            advanced.Add(schedule.Advance(now));
        }

        _schedules = advanced;
    }

    private async Task RunOne(ServiceSchedule schedule, ScheduledRun run, CancellationToken ct)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var service = scope.ServiceProvider.GetServices<IScheduledService>().FirstOrDefault(s => s.Identity() == schedule.Name);
            if (service is null)
            {
                LogUnknownService(log, schedule.Name.Value);
                return;
            }

            try
            {
                var result = await service.Run(run, ct).ConfigureAwait(false);
                if (result.IsError)
                {
                    LogServiceFailed(log, schedule.Name.Value, result.Error.Code, result.Error.Message);
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: one failed run (its own timeout too) must not stop later slots
            {
                var failed = SindriErrors.ServiceFailed.With(e.Message);
                LogServiceFailed(log, schedule.Name.Value, failed.Code, failed.Message);
            }
        }
    }

    private async Task Delay(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, clock, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Schedule loaded from approved profile {Profile}: {Count} services")]
    private static partial void LogSchedule(ILogger logger, string profile, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 the approved profile couldn't be read: {Code} {Detail}; keeping the current schedule")]
    private static partial void LogRefreshFailed(ILogger logger, string code, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 the profile names service '{Service}', which this build doesn't have")]
    private static partial void LogUnknownService(ILogger logger, string service);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 service '{Service}' failed: {Code} {Detail}")]
    private static partial void LogServiceFailed(ILogger logger, string service, string code, string detail);
}
