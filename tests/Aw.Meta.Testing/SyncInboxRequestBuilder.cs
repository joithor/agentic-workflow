using Aw.Meta.Core.Ids;
using Aw.Meta.Features.SyncInbox;

namespace Aw.Meta.Testing;

/// <summary>Builds a SyncInboxRequest with deterministic defaults.</summary>
public sealed class SyncInboxRequestBuilder
{
    private SessionId _session = SessionId.From("session-1");
    private string _transcript = "/transcripts/meta.jsonl";

    /// <summary>Sets the session.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SyncInboxRequestBuilder WithSession(SessionId value)
    {
        _session = value;
        return this;
    }

    /// <summary>Sets the transcript path.</summary>
    /// <param name="value">The path.</param>
    /// <returns>This builder.</returns>
    public SyncInboxRequestBuilder WithTranscript(string value)
    {
        _transcript = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SyncInboxRequest Build() => new(_session, _transcript);
}
