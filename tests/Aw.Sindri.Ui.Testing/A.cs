using System.Globalization;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>Builders for the API records, with the defaults of a healthy control plane.</summary>
internal static class A
{
    internal const string Hash = "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12";

    internal const string Changed = "ff00ee11dd22cc33bb44aa5599886677ff00ee11dd22cc33bb44aa5599886677";

    internal static readonly DateTimeOffset Noon = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    internal static ControlRecord Record(int pid = 4242, long epoch = 7, int port = 7410) => new(new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/")), pid, epoch);

    internal static ProfileView Approved(string source = "/home/profile.yaml") => new(Hash, Hash, ChangedSinceApproval: false, Problem: null, source, NeedsApproval: false);

    internal static ProfileView ChangedProfile(string source = "/home/profile.yaml") => new(Hash, Changed, ChangedSinceApproval: true, Problem: null, source, NeedsApproval: true);

    internal static ProfileView Unapproved(string source = "/home/profile.yaml") => new(Approved: null, Hash, ChangedSinceApproval: false, Problem: null, source, NeedsApproval: true);

    internal static ServiceView Heartbeat(DateTimeOffset? lastRun = null, string every = "5s") => new("heartbeat", every, lastRun);

    internal static StatusView Status(int pid = 4242, long epoch = 7, ProfileView? profile = null, params ServiceView[] services) =>
        new(Running: true, pid, epoch, Noon.AddSeconds(60), profile ?? Approved(), services.Length == 0 ? [Heartbeat(Noon)] : services);

    internal static EventView Event(long sequence, string type = "service.heartbeat", DateTimeOffset? at = null) => new(sequence, type, at ?? Noon.AddSeconds(sequence));
}
