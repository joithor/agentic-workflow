using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.CheckProfile;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// The profile watch (API spec C8): while the process holds the lease it checks the live profile against the approved
/// one at start and then every 30 s, as the caller 'service:profile-watch', and records 'profile.checked' when the pair
/// changed. A failed check is logged once per distinct problem and retried on the next tick.
/// </summary>
internal sealed partial class ProfileWatch(
    IServiceScopeFactory scopes, HeldLease held, ICallerContextAccessor callers, TimeProvider clock, ILogger<ProfileWatch> log)
    : BackgroundService
{
    /// <summary>How often a check runs while the lease is held.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan _idle = TimeSpan.FromSeconds(1);
    private string? _problem;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var grant = held.Current;
            if (grant is not null)
            {
                await Check(grant, stoppingToken).ConfigureAwait(false);
            }

            await Delay(grant is null ? _idle : Every, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task Check(LeaseGrant grant, CancellationToken ct)
    {
        using var caller = callers.EnterService("profile-watch");
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            Result<CheckProfileResponse> checkedNow;
            try
            {
                var service = scope.ServiceProvider.GetRequiredService<IService<CheckProfileRequest, CheckProfileResponse>>();
                checkedNow = await service.Handle(new CheckProfileRequest(grant), ct).ConfigureAwait(false);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: one failed check must not stop the watch
            {
                checkedNow = Core.SindriErrors.ServiceFailed.With(e.Message);
            }

            Report(checkedNow);
        }
    }

    private void Report(Result<CheckProfileResponse> checkedNow)
    {
        if (checkedNow.IsSuccess)
        {
            _problem = null;
            if (checkedNow.Value.Sequence is { } sequence)
            {
                LogRecorded(log, sequence);
            }

            return;
        }

        if (!string.Equals(_problem, checkedNow.Error.Message, StringComparison.Ordinal))
        {
            _problem = checkedNow.Error.Message;
            LogFailed(log, checkedNow.Error.Code, checkedNow.Error.Message);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "profile.checked recorded at sequence {Sequence}")]
    private static partial void LogRecorded(ILogger logger, long sequence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 the profile watch couldn't check the profile: {Code} {Detail}")]
    private static partial void LogFailed(ILogger logger, string code, string detail);
}
