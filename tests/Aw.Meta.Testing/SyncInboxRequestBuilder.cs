using Aw.Meta.Core.Ids;
using Aw.Meta.Features.SyncInbox;

namespace Aw.Meta.Testing;

/// <summary>Builds a SyncInboxRequest with deterministic defaults.</summary>
public sealed class SyncInboxRequestBuilder
{
    private MessageId _messageId = MessageId.From("messageid-1");

    /// <summary>Sets MessageId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public SyncInboxRequestBuilder WithMessageId(MessageId value)
    {
        _messageId = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public SyncInboxRequest Build() => new(_messageId);
}
