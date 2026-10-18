using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A SyncInboxRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static SyncInboxRequestBuilder SyncInboxRequest() => new();

    /// <summary>The fixed start time every default timestamp counts from.</summary>
    public static DateTimeOffset T0 { get; } = new(2026, 10, 15, 4, 0, 0, TimeSpan.Zero);

    /// <summary>A time <paramref name="minutes"/> after <see cref="T0"/>.</summary>
    /// <param name="minutes">Minutes after T0.</param>
    /// <returns>The time.</returns>
    public static DateTimeOffset At(int minutes) => T0.AddMinutes(minutes);

    /// <summary>A transcript line for an assistant turn whose last API call used the given context.</summary>
    /// <param name="tokens">The context tokens, counted as input tokens.</param>
    /// <returns>The JSONL line without its line ending.</returns>
    public static string UsageLine(long tokens) =>
        "{\"type\":\"assistant\",\"message\":{\"role\":\"assistant\",\"usage\":{\"input_tokens\":" + tokens.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"output_tokens\":5}}}";

    /// <summary>An inbound message with deterministic defaults.</summary>
    /// <param name="id">The message id.</param>
    /// <param name="name">The sender's session name.</param>
    /// <param name="body">The text.</param>
    /// <param name="minute">Minutes after T0.</param>
    /// <returns>The classified message.</returns>
    public static InboxMessage Inbox(string id, string name = "api-1", string body = "hello", int minute = 0) =>
        InboxMessage.Of(new PeerMessage(MessageId.From(id), $"uds:/run/{name}.sock", name, body, At(minute)));

    /// <summary>A message the meta sent.</summary>
    /// <param name="to">The recipient.</param>
    /// <param name="message">The text.</param>
    /// <param name="minute">Minutes after T0.</param>
    /// <returns>The outbound record.</returns>
    public static Outbound Sent(string to, string message, int minute) => new(to, message, At(minute));

    /// <summary>A live session.</summary>
    /// <param name="name">The session name.</param>
    /// <param name="session">The session id.</param>
    /// <returns>The session.</returns>
    public static LiveSession Live(string name, string session) => new(SessionId.From(session), name, Pid: 1000, Status: "idle", Cwd: "/work");

    /// <summary>A lane assignment.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="sessionName">The session name.</param>
    /// <returns>The assignment.</returns>
    public static LaneAssignment Assigned(string lane, string sessionName) => new(lane, sessionName, Session: null, LaneSource.Direct);

    /// <summary>Obligation facts with nothing recorded, at <see cref="T0"/> plus an hour.</summary>
    /// <returns>The facts.</returns>
    public static ObligationFacts Facts() => new()
    {
        Messages = [],
        Outbound = [],
        Lanes = LaneMapState.Empty,
        MetaLane = "meta",
        Now = At(60),
    };
}
