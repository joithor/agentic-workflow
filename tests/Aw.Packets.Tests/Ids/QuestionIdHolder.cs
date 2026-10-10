using Aw.Packets.Core.Ids;

namespace Aw.Packets.Tests.Ids;

/// <summary>Wraps a QuestionId so the JSON tests can send a missing or null property.</summary>
public sealed record QuestionIdHolder(QuestionId Id);
