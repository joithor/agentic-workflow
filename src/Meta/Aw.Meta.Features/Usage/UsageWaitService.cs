using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Features.Usage;

/// <summary>Blocks until every limited lane (and the meta) can work again, or the time runs out.</summary>
/// <param name="config">The team config.</param>
/// <param name="reader">Looks at the lanes and the meta.</param>
/// <param name="delay">Waits between looks.</param>
/// <param name="clock">The current time.</param>
internal sealed class UsageWaitService(IMetaConfig config, UsageReader reader, IDelay delay, TimeProvider clock) : IService<UsageWaitRequest, UsageWaitResponse>
{
    internal static readonly TimeSpan Poll = TimeSpan.FromSeconds(60);

    public async ValueTask<Result<UsageWaitResponse>> Handle(UsageWaitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var start = clock.GetUtcNow();
        var readings = await reader.Read(team.Value, cancellationToken).ConfigureAwait(false);
        var waiting = readings.IsError ? [] : Limited(readings.Value).Select(static r => r.Subject).ToList();
        while (readings.IsSuccess && waiting.Count > 0)
        {
            var now = clock.GetUtcNow();
            var limited = Limited(readings.Value);
            var unreadable = Unreadable(readings.Value, waiting);
            if (unreadable.Count == 0 && Recovered(limited, request, start, now, team.Value.Usage))
            {
                return new UsageWaitResponse(Ok: true, OkLines(waiting), Seconds(start, now));
            }

            if (now - start >= request.Max)
            {
                var minutes = ((long)request.Max.TotalMinutes).ToString(CultureInfo.InvariantCulture);
                return new UsageWaitResponse(Ok: false, [$"USAGE WAIT TIMEOUT after {minutes}m: still limited: {string.Join(", ", limited.Select(static r => r.Subject).Concat(unreadable))}"], Seconds(start, now));
            }

            await delay.Wait(TimeSpan.FromTicks(Math.Min(Poll.Ticks, (request.Max - (now - start)).Ticks)), cancellationToken).ConfigureAwait(false);
            readings = await reader.Read(team.Value, cancellationToken).ConfigureAwait(false);
        }

        return readings.IsError ? readings.Error : new UsageWaitResponse(Ok: true, ["USAGE OK nothing is limited"], WaitedSeconds: 0);
    }

    private static List<string> Unreadable(IReadOnlyList<UsageReading> readings, List<string> waiting) =>
        [.. waiting.Where(w => readings.Any(r => r.State == UsageState.Unknown && string.Equals(r.Subject, w, StringComparison.Ordinal)))];

    private static bool Recovered(List<UsageReading> limited, UsageWaitRequest request, DateTimeOffset start, DateTimeOffset now, UsageConfig usage) =>
        limited.Count == 0 || (!request.UntilOk && limited.TrueForAll(r => Due(r, start, now, usage)));

    private static List<UsageReading> Limited(IReadOnlyList<UsageReading> readings) => [.. readings.Where(static r => r.State == UsageState.Limited)];

    private static bool Due(UsageReading reading, DateTimeOffset start, DateTimeOffset now, UsageConfig usage) =>
        reading.Stop?.ResetsAt is { } at ? now >= at + TimeSpan.FromSeconds(usage.ResumeMarginSeconds) : now >= start + TimeSpan.FromMinutes(usage.ProbeMinutes);

    private static long Seconds(DateTimeOffset start, DateTimeOffset now) => (long)(now - start).TotalSeconds;

    private static List<string> OkLines(IReadOnlyList<string> subjects)
    {
        var lines = new List<string>();
        foreach (var subject in subjects)
        {
            lines.Add($"USAGE OK {subject}");
            if (string.Equals(subject, UsageWatch.Meta, StringComparison.Ordinal))
            {
                lines.Add(UsageWatch.MetaWake);
            }
        }

        return lines;
    }
}
