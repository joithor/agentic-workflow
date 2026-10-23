using System.Text.Json;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CodeIndexJobs;
using Aw.Sindri.Testing;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.CodeIndexJobs;

/// <summary>The shared set-up of the code index service and outlooks tests: a profile with two repos, a scripted index and heads, and the jobs view in memory.</summary>
internal sealed class CodeIndexJobsRig
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    internal static readonly CommitSha Old = CommitSha.From(new string('1', 40));
    internal static readonly CommitSha New = CommitSha.From(new string('2', 40));
    internal static readonly RepoName Demo = RepoName.From("demo");
    internal static readonly RepoName Other = RepoName.From("other");
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);
    private static readonly string[] _embeddingsPending = ["embeddings"];
    private int _next;

    internal SliceHarness Harness { get; } = new();

    internal InMemoryJobViews Jobs { get; } = new();

    internal FakeCodeIndex Index { get; } = new();

    internal FakeRepoHeads Heads { get; } = new();

    internal CodeIndexJobScopes Scopes => new(Harness.Profiles, Harness.Callers);

    internal async Task Approve(CancellationToken cancellationToken)
    {
        Harness.Clock.SetUtcNow(Now);
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("demo", "/work/demo"), new RepoEntry("other", "/work/other")]);
        _ = await Harness.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), cancellationToken);
    }

    /// <summary>Adds a BuildCodeIndex job of the repo's key, ended <paramref name="endedAgo"/> ago (or running).</summary>
    internal void Job(string repo, JobState state, TimeSpan endedAgo, string? outcome = null, int? embedded = null, bool pending = false)
    {
        _next++;
        var result = outcome is null ? (JsonElement?)null : JsonSerializer.SerializeToElement(new { repo, outcome, embedded = embedded ?? 0, layersPending = pending ? _embeddingsPending : (IReadOnlyList<string>)[] }, _web);
        Jobs.Put(new JobRow(JobId.New(Harness.Ids), JobKind.From("BuildCodeIndex"), JobKey.From("index:" + repo), Item: null, Repo: null, Service: null, state,
            "service:code-index-main-move", Now - endedAgo - TimeSpan.FromSeconds(5), state == JobState.Running ? null : Now - endedAgo, [], Rounds: 0, Summary: null,
            state == JobState.Failed ? "SND-CODEIDX-002" : null, result, CancelledBy: null, Sequence: _next));
    }
}
