namespace Aw.Sindri.Core.Scheduling;

/// <summary>One ended embed job as <see cref="EmbedDecision"/> reads it.</summary>
/// <param name="Kind">How it left the repo.</param>
/// <param name="EndedAt">When it ended.</param>
public sealed record EmbedAttempt(EmbedAttemptKind Kind, DateTimeOffset EndedAt);
