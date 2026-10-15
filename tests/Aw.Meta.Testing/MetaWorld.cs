using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Testing;

/// <summary>Every port as its in-memory fake, the shared coordination services over them and a clock: what a slice test needs. A new instance is a fresh machine.</summary>
public sealed class MetaWorld
{
    /// <summary>The meta session's transcript path.</summary>
    public const string Transcript = "/transcripts/meta.jsonl";

    /// <summary>The registered meta session id.</summary>
    public const string MetaSessionId = "s-meta";

    /// <summary>The clock, which starts an hour after <see cref="A.T0"/>.</summary>
    public ManualClock Clock { get; } = new(A.At(60));

    /// <summary>Messages, offsets and the role.</summary>
    public InMemoryMessageStore Messages { get; } = new();

    /// <summary>The lane map.</summary>
    public InMemoryLaneStore Lanes { get; } = new();

    /// <summary>Owner questions.</summary>
    public InMemoryOwnerStore Owner { get; } = new();

    /// <summary>Gates and merges.</summary>
    public InMemoryGateStore Gates { get; } = new();

    /// <summary>Small values.</summary>
    public InMemoryKeyValueStore Values { get; } = new();

    /// <summary>The team config.</summary>
    public InMemoryMetaConfig Config { get; } = new();

    /// <summary>Live sessions.</summary>
    public InMemorySessionDirectory Sessions { get; } = new();

    /// <summary>The running log.</summary>
    public InMemoryRunningLog Log { get; } = new();

    /// <summary>Transcript files.</summary>
    public InMemoryTranscriptReader Transcripts { get; } = new();

    /// <summary>The inbox sync.</summary>
    public InboxSync Sync => new(Transcripts, Messages, Owner, Clock);

    /// <summary>The lane refresher.</summary>
    public LaneRefresher Refresher => new(Sessions, Messages, Lanes);

    /// <summary>The obligation reader.</summary>
    public ObligationReader Reader => new(Messages, Lanes, Owner, Gates, Values);

    /// <summary>The meta-session check.</summary>
    public MetaSession Session => new(Messages, Config);

    /// <summary>The stop-check sweep.</summary>
    public MetaSweep Sweep => new(Sync, Refresher, Reader);

    /// <summary>The loop guard.</summary>
    public StopGuard Guard => new(Values, Log);

    /// <summary>Saves a config for team "sindri" with the lanes meta, api and ui, and registers the meta session.</summary>
    /// <returns>A task.</returns>
    public async Task Ready()
    {
        _ = await Config.Save(new TeamConfig("sindri", "/work", ["meta", "api", "ui"], "meta"), CancellationToken.None);
        _ = await Messages.SetRole(new MetaRole(SessionId.From(MetaSessionId), "sindri-meta"), CancellationToken.None);
    }

    /// <summary>The open obligations now.</summary>
    /// <returns>The open set.</returns>
    public async Task<IReadOnlyList<Obligation>> Open() =>
        (await Reader.Open("meta", Clock.GetUtcNow(), CancellationToken.None)).Value;

    /// <summary>A transcript line holding a peer message.</summary>
    /// <param name="id">The message id.</param>
    /// <param name="name">The sender's session name.</param>
    /// <param name="body">The text.</param>
    /// <returns>The JSONL line with its line ending.</returns>
    public static string PeerLine(string id, string name, string body) =>
        $$"""{"type":"user","origin":{"kind":"peer","from":"uds:/run/{{name}}.sock","msg_id":"{{id}}","name":"{{name}}","body":"{{body}}"},"timestamp":"2026-10-15T04:05:00.000Z"}""" + "\n";
}
