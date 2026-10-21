namespace Aw.Sindri.Core.Scheduling;

/// <summary>A repo's outlook with the owner's word for it and, when a retry is due later, when.</summary>
/// <param name="Outlook">The classification.</param>
/// <param name="Word">The per-repo word.</param>
/// <param name="RetryAt">When the next attempt is due, for <see cref="IndexOutlook.Backoff"/>; otherwise null.</param>
public sealed record IndexOutlookDetail(IndexOutlook Outlook, RepoWait Word, DateTimeOffset? RetryAt);
