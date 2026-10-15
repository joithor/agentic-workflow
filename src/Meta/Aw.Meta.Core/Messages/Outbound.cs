using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Messages;

/// <summary>A message the meta session sent.</summary>
/// <param name="To">The recipient as the send addressed it: a session name, id or socket.</param>
/// <param name="Message">The message text.</param>
/// <param name="At">When it was sent.</param>
public sealed record Outbound(string To, string Message, DateTimeOffset At)
{
    /// <summary>The message id the send returned, when known.</summary>
    public MessageId? Id { get; init; }
}
