using Aw.Meta.Core.Ids;

namespace Aw.Meta.Tests.Ids;

/// <summary>Wraps a MessageId so the JSON tests can send a missing or null property.</summary>
public sealed record MessageIdHolder(MessageId Id);
