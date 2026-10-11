using System.Globalization;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// How the window words Sindri's answers. Presentation only: every verdict (running, needs approval, changed) comes from
/// Sindri's status record; this picks the words, the same words 'sindri status' uses.
/// </summary>
internal static class StatusText
{
    internal const string ApproveFix = "sindri profile approve";

    // The health line (§4.8): Running · pid · epoch · API v1 · N services · profile <state>, the fields the status carries.
    internal static string HealthLine(StatusView s) => string.Join(" · ", HealthWords(s));

    internal static string Lease(StatusView s) => string.Create(CultureInfo.InvariantCulture, $"pid {s.Pid} · epoch {s.Epoch}");

    // "approved" only when Sindri says nothing needs approval; otherwise the most specific reason the record gives.
    internal static string ProfileState(ProfileView p) => p switch
    {
        { Approved: null, Problem: not null } => "unreadable",
        { Approved: null } => "not approved",
        { ChangedSinceApproval: true } => "changed since approval",
        { NeedsApproval: true } => "needs approval",
        _ => "approved",
    };

    internal static string ProfileDetail(ProfileView p) => p switch
    {
        { Approved: null, Problem: { } problem } => problem,
        { Approved: null } => $"{p.Source} (hash {Short(p.Live)})",
        { ChangedSinceApproval: true } => $"running with approved {Short(p.Approved)}; {p.Source} is now {Short(p.Live)}",
        { Problem: { } problem } => $"hash {Short(p.Approved)}; the live file has a problem: {problem}",
        _ => $"hash {Short(p.Approved)} · {p.Source}",
    };

    // Approving needs a live profile that loads: offer the command only when it can work (approve refuses an unreadable one).
    internal static string ProfileFix(ProfileView p) => p.NeedsApproval && p.Live is not null ? ApproveFix : string.Empty;

    internal static ServiceRow Service(ServiceView s) => new(s.Name, "every " + s.Every, s.LastRun is { } t ? "last run " + Time(t) : "not run yet");

    internal static string Event(EventView e) => string.Create(CultureInfo.InvariantCulture, $"{e.Type} · {Time(e.OccurredAt)} · #{e.Sequence}");

    private static IEnumerable<string> HealthWords(StatusView s)
    {
        yield return Labels.Running;
        yield return Lease(s);
        if (s.ApiVersion is { } version)
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"API v{version}");
        }

        yield return s.Services.Count == 1 ? "1 service" : string.Create(CultureInfo.InvariantCulture, $"{s.Services.Count} services");
        yield return "profile " + ProfileState(s.Profile);
    }

    private static string Time(DateTimeOffset t) => t.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "Z";

    private static string Short(string? hash) => hash is { Length: >= 6 } ? hash[..6] : hash ?? "none";
}
