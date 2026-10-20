using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'repo.added' (setup-once spec §7.5): the path is not public.</summary>
/// <param name="CommandId">The 'AddRepo' command's id.</param>
/// <param name="Repo">The repo's name in the profile.</param>
public sealed record RepoAddedPublic(CommandId CommandId, RepoName Repo);
