using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Features.ListItems;

/// <summary>List items (GET /sindri/items?state=&amp;repo=; API spec §3.1): open items unless <paramref name="State"/> asks for closed ones.</summary>
/// <param name="State">Only items in this state, or null.</param>
/// <param name="Repo">Only items in this repo, or null.</param>
public sealed record ListItemsRequest(ItemState? State, RepoName? Repo) : IQuery<ListItemsResponse>;
