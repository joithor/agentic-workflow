namespace Aw.CodeIndex.Core.Planning;

/// <summary>What one ended build job of a repo says about continuing (spec §6.3): counts and words from its result.</summary>
/// <param name="EndedAt">When it ended.</param>
/// <param name="Failed">True when the job failed.</param>
/// <param name="Deferred">True when it ended deferred (every heavy-job slot stayed busy: nothing published).</param>
/// <param name="LayersPending">True when the result recorded a pending layer (embeddings).</param>
/// <param name="Embedded">Symbols with a vector after the job; 0 when the result has none.</param>
/// <param name="Commit">The commit the job published, or null: embedded counts of jobs at different commits are not compared.</param>
public sealed record IndexJobOutcome(DateTimeOffset EndedAt, bool Failed, bool Deferred, bool LayersPending, int Embedded, string? Commit = null);
