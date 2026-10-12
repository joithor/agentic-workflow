using System.Globalization;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>Builders for the API records, with the defaults of a healthy control plane.</summary>
internal static class A
{
    internal const string Hash = "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12";

    internal const string Changed = "ff00ee11dd22cc33bb44aa5599886677ff00ee11dd22cc33bb44aa5599886677";

    internal static readonly DateTimeOffset Noon = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A per-start human token, as the file holds it once trimmed (a stand-in; serve mints base64url).</summary>
    internal const string HumanToken = "this-start-human-token";

    /// <summary>Sindri's 403 for a human call without this start's token: the title and detail of its golden (UI spec E-17).</summary>
    internal static readonly (string Code, string Detail) NoTokenRefusal = Refusal("human-no-token");

    /// <summary>Sindri's 403 for a human call with another start's token: the title and detail of its golden.</summary>
    internal static readonly (string Code, string Detail) StaleTokenRefusal = Refusal("human-stale-token");

    internal static ControlRecord Record(int pid = 4242, long epoch = 7, int port = 7410) => new(new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/")), pid, epoch);

    internal static ProfileView Approved(string source = "/home/profile.yaml") => new(Hash, Hash, ChangedSinceApproval: false, Problem: null, source, NeedsApproval: false);

    internal static ProfileView ChangedProfile(string source = "/home/profile.yaml") => new(Hash, Changed, ChangedSinceApproval: true, Problem: null, source, NeedsApproval: true);

    internal static ProfileView Unapproved(string source = "/home/profile.yaml") => new(Approved: null, Hash, ChangedSinceApproval: false, Problem: null, source, NeedsApproval: true);

    internal static ServiceView Heartbeat(DateTimeOffset? lastRun = null, string every = "5s") => new("heartbeat", every, lastRun);

    internal static StatusView Status(int pid = 4242, long epoch = 7, ProfileView? profile = null, params ServiceView[] services) =>
        new(Running: true, pid, epoch, Noon.AddSeconds(60), profile ?? Approved(), services.Length == 0 ? [Heartbeat(Noon)] : services);

    // A refusal golden's title and detail, so no test copies Sindri's sentence (SND-AUTH-001's text changes in P2 unit F).
    private static (string Code, string Detail) Refusal(string scenario)
    {
        var problem = AwJson.Deserialize<ProblemView>(Encoding.UTF8.GetBytes(GoldenFiles.Api("status", scenario)), SindriApiClient.Json).Value;
        return (problem.Title!, problem.Detail!); // why: Sindri's refusal goldens carry both; GoldenContractTests checks it
    }

    internal static EventView Event(long sequence, string type = "service.heartbeat", DateTimeOffset? at = null) => new(sequence, type, at ?? Noon.AddSeconds(sequence));
}
