using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The ledger's repo of an item, as the block runner reads it for a child instance: the repos it was given by item key, or an error for every read.</summary>
internal sealed class ItemReposStub : IItemRepos
{
    private readonly Dictionary<string, string> _repos = new(StringComparer.Ordinal);
    private readonly bool _fails;

    internal ItemReposStub(bool fails = false)
    {
        _fails = fails;
    }

    internal ItemReposStub With(string item, string repo)
    {
        _repos[item] = repo;
        return this;
    }

    public ValueTask<Result<RepoName?>> RepoOf(ItemId item, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<RepoName?>>(_fails
            ? SindriErrors.ApiInternal.With("the ledger is unreadable")
            : _repos.TryGetValue(item.Value, out var repo) ? RepoName.From(repo) : (RepoName?)null);

    public ValueTask<Result<bool>> Exists(ItemId item, CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(_repos.ContainsKey(item.Value));
}
