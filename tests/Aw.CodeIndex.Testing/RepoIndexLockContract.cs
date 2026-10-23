using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.CodeIndex.Testing;

/// <summary>What every <see cref="IRepoIndexLock"/> honours: one holder per repo, another repo unaffected, a refusal with SND-CODEIDX-016 while held, and a release (even a repeated one) that frees the repo.</summary>
public abstract class RepoIndexLockContract
{
    private static readonly RepoName _repo = RepoName.From("alpha");
    private static readonly RepoName _other = RepoName.From("beta");

    /// <summary>A lock with nothing held.</summary>
    /// <returns>The lock.</returns>
    protected abstract IRepoIndexLock Create();

    /// <summary>A free repo is taken.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_free_repo_is_taken()
    {
        var taken = await Create().Take(_repo, TimeSpan.Zero, CancellationToken.None);

        Require(taken.IsSuccess, "a free repo is taken");
        await taken.Value.DisposeAsync();
    }

    /// <summary>A held repo refuses a second taker with SND-CODEIDX-016.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_held_repo_is_refused_while_held()
    {
        var index = Create();
        var first = await index.Take(_repo, TimeSpan.Zero, CancellationToken.None);

        var second = await index.Take(_repo, TimeSpan.Zero, CancellationToken.None);

        Require(second.IsError && string.Equals(second.Error.Code, CodeIndexErrors.IndexBusy.Code, StringComparison.Ordinal), "a held repo answers SND-CODEIDX-016");
        await first.Value.DisposeAsync();
    }

    /// <summary>Another repo is not blocked.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Repos_do_not_block_each_other()
    {
        var index = Create();
        var first = await index.Take(_repo, TimeSpan.Zero, CancellationToken.None);

        var other = await index.Take(_other, TimeSpan.Zero, CancellationToken.None);

        Require(other.IsSuccess, "another repo is taken while the first is held");
        await first.Value.DisposeAsync();
        await other.Value.DisposeAsync();
    }

    /// <summary>Disposing frees the repo, and disposing twice does not free a later holder.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_release_frees_the_repo_once()
    {
        var index = Create();
        var first = (await index.Take(_repo, TimeSpan.Zero, CancellationToken.None)).Value;
        await first.DisposeAsync();
        var second = await index.Take(_repo, TimeSpan.Zero, CancellationToken.None);
        await first.DisposeAsync();

        var third = await index.Take(_repo, TimeSpan.Zero, CancellationToken.None);

        Require(second.IsSuccess, "the repo is free after a release");
        Require(third.IsError, "a repeated release does not free the next holder");
        await second.Value.DisposeAsync();
    }

    private static void Require(bool condition, string what)
    {
        if (!condition)
        {
            throw new ContractViolationException($"contract: {what}");
        }
    }
}
