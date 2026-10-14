namespace Aw.Sindri.Core.Events;

/// <summary>One repo of a 'context.sources.synced' event.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Head">The commit its index now reflects, or null when it couldn't be read.</param>
/// <param name="Added">Documents indexed anew.</param>
/// <param name="Removed">Documents dropped.</param>
/// <param name="Kept">Documents unchanged.</param>
/// <param name="Blocked">Documents the scrubber withheld.</param>
/// <param name="Code">The error code when the repo couldn't be refreshed.</param>
/// <param name="Skipped">Documents not indexed this run: over the size cap, or unreadable (their earlier rows stay).</param>
/// <param name="Recleaned">Documents cleaned again because the scrub rules changed and indexed with text (so among Added); one the new rules block or one over the size cap counts as Blocked or Skipped instead.</param>
public sealed record ContextRepoSynced(string Repo, string? Head, int Added, int Removed, int Kept, int Blocked, string? Code, int Skipped = 0, int Recleaned = 0);
