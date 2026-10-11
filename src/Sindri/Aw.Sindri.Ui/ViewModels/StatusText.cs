using System.Globalization;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// How the window words Sindri's answers. Presentation only: every verdict (running, needs approval, changed) comes from
/// Sindri's status record; this picks the words, the same words 'sindri status' uses.
/// </summary>
internal static class StatusText
{
    internal const string StartFix = "sindri";
    internal const string ApproveFix = "sindri profile approve";

    internal static string Lease(StatusView s) => string.Create(CultureInfo.InvariantCulture, $"pid {s.Pid} · epoch {s.Epoch}");

    internal static string ProfileState(ProfileView p) => p switch
    {
        { Approved: null, Problem: not null } => "unreadable",
        { Approved: null } => "not approved",
        { ChangedSinceApproval: true } => "changed since approval",
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

    internal static string ProfileFix(ProfileView p) => p.NeedsApproval ? ApproveFix : string.Empty;

    internal static ServiceRow Service(ServiceView s) => new(s.Name, "every " + s.Every, s.LastRun is { } t ? "last run " + Time(t) : "not run yet");

    internal static string Event(EventView e) => string.Create(CultureInfo.InvariantCulture, $"{e.Type} · {Time(e.OccurredAt)} · #{e.Sequence}");

    private static string Time(DateTimeOffset t) => t.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "Z";

    private static string Short(string? hash) => hash is { Length: >= 6 } ? hash[..6] : hash ?? "none";
}
