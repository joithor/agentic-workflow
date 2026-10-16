using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>Where each item belongs, fixed.</summary>
internal sealed class FixedRepos(params (string Item, string? Repo)[] repos) : IItemRepos
{
    public ValueTask<Result<RepoName?>> RepoOf(ItemId item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(repos.FirstOrDefault(r => r.Item == item.Value).Repo is { } repo ? (RepoName?)RepoName.From(repo) : null));

    public ValueTask<Result<bool>> Exists(ItemId item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(repos.Any(r => r.Item == item.Value)));
}
