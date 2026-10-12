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

    /// <summary>The fixture's newest queued item ("Try the queue", version 1): its detail and timeline are served.</summary>
    internal const string NewItem = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a304";

    /// <summary>The fixture's needs-decision item.</summary>
    internal const string DecisionItem = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a305";

    /// <summary>An id no fixture serves: GET /sindri/items/{id} answers SND-ITEM-404.</summary>
    internal const string MissingItem = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a399";

    /// <summary>A spec-shaped refusal's detail as the golden serves it (tests key off the golden, never a copy of its sentence).</summary>
    /// <param name="slug">The route's slug.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>The detail.</returns>
    internal static string Detail(string slug, string scenario) =>
        System.Text.Json.JsonDocument.Parse(GoldenFiles.SpecShaped(slug, scenario)).RootElement.GetProperty("detail").GetString()!;
}
