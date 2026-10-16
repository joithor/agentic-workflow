using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'item.created' (API spec §5.3): the payload without its body.</summary>
/// <param name="CommandId">The 'CreateItem' command's id.</param>
/// <param name="Item">The new item.</param>
/// <param name="Title">Its title.</param>
/// <param name="Repo">The repo it belongs to, or null.</param>
/// <param name="Link">The tracker record it was created from, or null.</param>
/// <param name="Owner">Reserved for multi-user (README X61); always null for now.</param>
public sealed record ItemCreatedPublic(CommandId CommandId, ItemId Item, string Title, RepoName? Repo, RecordKey? Link, string? Owner);
