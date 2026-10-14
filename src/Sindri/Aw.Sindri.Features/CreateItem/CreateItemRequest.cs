using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>Create a work item (POST /sindri/items, 'sindri item add'; API spec §3.2): desk class, appends 'item.created'.</summary>
/// <param name="Title">The title: 1–200 characters after trimming.</param>
/// <param name="Repo">The repo it belongs to; null takes the caller's X-Sindri-Repo.</param>
/// <param name="Body">The body, or null: at most 4,000 characters, never public.</param>
/// <param name="Link">The tracker record it comes from, or null.</param>
public sealed record CreateItemRequest(string Title, RepoName? Repo, string? Body, RecordKey? Link) : ICommand<ItemCreatedResponse>;
