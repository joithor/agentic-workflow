using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.OutboxRecord;

/// <summary>The OutboxRecord request: a session sent a message to a peer.</summary>
/// <param name="Session">The session that sent it; only the registered meta session's sends are recorded.</param>
/// <param name="To">The recipient as the send addressed it: a session name, id or socket.</param>
/// <param name="Message">The message text.</param>
/// <param name="Id">The message id the send returned, when it showed one.</param>
public sealed record OutboxRecordRequest(SessionId Session, string To, string Message, MessageId? Id);
