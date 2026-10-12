namespace Aw.Sindri.Core.Events;

/// <summary>One repo of a 'context.sources.synced' event.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Head">The commit its index now reflects, or null when it couldn't be read.</param>
/// <param name="Added">Documents indexed anew.</param>
/// <param name="Removed">Documents dropped.</param>
/// <param name="Kept">Documents unchanged.</param>
/// <param name="Blocked">Documents the scrubber withheld.</param>
/// <param name="Code">The error code when the repo couldn't be refreshed.</param>
public sealed record ContextRepoSynced(string Repo, string? Head, int Added, int Removed, int Kept, int Blocked, string? Code);
