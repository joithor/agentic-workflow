using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Messages;

/// <summary>A message another session sent, as found in a transcript record.</summary>
/// <param name="Id">The message id the sender's delivery carries; the dedupe key.</param>
/// <param name="From">The sender's socket address.</param>
/// <param name="FromName">The sender's session name; empty when it had none.</param>
/// <param name="Body">The message text.</param>
/// <param name="At">When the transcript recorded it.</param>
public sealed record PeerMessage(MessageId Id, string From, string FromName, string Body, DateTimeOffset At);
