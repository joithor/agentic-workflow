using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RecordServiceRun;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Runs the background services the approved profile names, on their cadence (factory addendum §17.29.1). It re-reads
/// the approval once a second, so an approval takes effect without a restart; a failed run is logged and the next slot
/// still runs, and an approval it can't read is logged once (SND-SCHED-001) while the current schedule keeps running.
/// Nothing runs while the process holds no lease. Each run is its own caller, 'service:&lt;name&gt;', so its events carry
/// that actor (API spec §2.1). A run is recorded as 'service.ran' (C8) when it failed, did work, or is the first ok after a
/// failure; an idle run after an ok one writes nothing. The last recorded outcome of each service is kept in memory and,
/// after a restart, read back from the service's latest 'service.ran'.
/// </summary>
internal sealed partial class SchedulerHost(
    IServiceScopeFactory scopes, IApprovals approvals, HeldLease held, ICallerContextAccessor callers, TimeProvider clock, ILogger<SchedulerHost> log)
    : BackgroundService
{
    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);
    private IReadOnlyList<ServiceSchedule> _schedules = [];
    private ProfileHash? _profile;
    private string? _problem;
    private readonly Dictionary<string, ServiceOutcome> _last = new(StringComparer.Ordinal);   // the last recorded outcome per service; absent = not known yet

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
        if (profile?.Hash != _profile)
        {
            _profile = profile?.Hash;
            _schedules = profile is null ? [] : ServiceSchedule.From(profile.Profile, clock.GetUtcNow());
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

            await RunOne(schedule, new ScheduledRun(grant, schedule.NextSlot), ct).ConfigureAwait(false);
            advanced.Add(schedule.Advance(now));
        }

        _schedules = advanced;
    }

    private async Task RunOne(ServiceSchedule schedule, ScheduledRun run, CancellationToken ct)
    {
        using var caller = callers.EnterService(schedule.Name.Value);
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outcome = await Outcome(scope.ServiceProvider, schedule.Name, run, ct).ConfigureAwait(false);
            if (await Skip(scope.ServiceProvider, schedule.Name, outcome, ct).ConfigureAwait(false))
            {
                return;
            }

            var recorded = await Record(scope.ServiceProvider, new RecordServiceRunRequest(schedule.Name, run, outcome.Map(_ => Unit.Value)), ct).ConfigureAwait(false);
            if (recorded)
            {
                _last[schedule.Name.Value] = outcome.IsError ? ServiceOutcome.Failed : ServiceOutcome.Ok;
            }
            else
            {
                _ = _last.Remove(schedule.Name.Value);   // what the ledger holds is unknown now; read it back if the next run is idle
            }
        }
    }

    /// <summary>Runs the service and returns how it ended: its result, a throw as SND-SCHED-001, or SND-SCHED-001 when this build lacks it (G8).</summary>
    private async Task<Result<ScheduledResult>> Outcome(IServiceProvider services, ServiceName name, ScheduledRun run, CancellationToken ct)
    {
        var service = services.GetServices<IScheduledService>().FirstOrDefault(s => s.Identity() == name);
        if (service is null)
        {
            LogUnknownService(log, name.Value);
            return SindriErrors.ServiceFailed.With($"the profile names service '{name.Value}', which this build doesn't have");
        }

        try
        {
            var result = await service.Run(run, ct).ConfigureAwait(false);
            if (result.IsError)
            {
                LogServiceFailed(log, name.Value, result.Error.Code, result.Error.Message);
            }

            return result;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: one failed run (its own timeout too) must not stop later slots
        {
            var failed = SindriErrors.ServiceFailed.With(e.Message);
            LogServiceFailed(log, name.Value, failed.Code, failed.Message);
            return failed;
        }
    }

    /// <summary>Appends the run's 'service.ran' (API spec C8); a failure to record is logged and the schedule goes on.</summary>
    private async Task<bool> Record(IServiceProvider services, RecordServiceRunRequest request, CancellationToken ct)
    {
        Result<RecordServiceRunResponse> recorded;
        try
        {
            recorded = await services.GetRequiredService<IService<RecordServiceRunRequest, RecordServiceRunResponse>>().Handle(request, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: recording one run must not stop later slots
        {
            recorded = SindriErrors.LedgerUnavailable.With(e.Message);
        }

        if (recorded.IsError)
        {
            LogRecordFailed(log, request.Service.Value, recorded.Error.Code, recorded.Error.Message);
        }

        return recorded.IsSuccess;
    }

    /// <summary>True for an idle run of a service whose last recorded outcome is ok: it writes nothing (C8).</summary>
    private async Task<bool> Skip(IServiceProvider services, ServiceName name, Result<ScheduledResult> outcome, CancellationToken ct)
    {
        if (outcome.IsError || !outcome.Value.Idle)
        {
            return false;
        }

        if (!_last.TryGetValue(name.Value, out var last))
        {
            var restored = await Restore(services, name, ct).ConfigureAwait(false);
            if (restored is null)
            {
                return false;
            }

            last = restored.Value;
            _last[name.Value] = last;
        }

        return last == ServiceOutcome.Ok;
    }

    /// <summary>The outcome of <paramref name="name"/>'s latest 'service.ran' in the ledger, or null when it has none or the ledger can't be read (then the run is recorded).</summary>
    private async Task<ServiceOutcome?> Restore(IServiceProvider services, ServiceName name, CancellationToken ct)
    {
        var reader = services.GetService<ILedgerReader>();
        var events = services.GetService<EventFactory>();
        if (reader is null || events is null)
        {
            return null;
        }

        ServiceOutcome? latest = null;
        try
        {
            for (var after = 0L; ;)
            {
                var page = await reader.ReadAfter(after, 500, ct).ConfigureAwait(false);
                if (page.IsError || page.Value.Count == 0)
                {
                    return page.IsError ? null : latest;
                }

                foreach (var e in page.Value.Where(e => string.Equals(e.Envelope.Type, EventTypes.ServiceRan, StringComparison.Ordinal)))
                {
                    var ran = events.Payload<ServiceRan>(e);
                    if (ran.IsSuccess && ran.Value.Service == name)
                    {
                        latest = ran.Value.Outcome;
                    }
                }

                after = page.Value[^1].Sequence;
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: an unreadable ledger must not stop the schedule; the run is recorded instead
        {
            LogRestoreFailed(log, name.Value, e.Message);
            return null;
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 service '{Service}' ran but its service.ran wasn't recorded: {Code} {Detail}")]
    private static partial void LogRecordFailed(ILogger logger, string service, string code, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 service '{Service}': its last recorded run couldn't be read back ({Detail}); recording this run")]
    private static partial void LogRestoreFailed(ILogger logger, string service, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 service '{Service}' failed: {Code} {Detail}")]
    private static partial void LogServiceFailed(ILogger logger, string service, string code, string detail);
}
