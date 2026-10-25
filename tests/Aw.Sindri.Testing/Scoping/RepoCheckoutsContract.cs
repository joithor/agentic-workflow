using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scoping;
using Xunit;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The contract every <see cref="IRepoCheckouts"/> honours.</summary>
public abstract class RepoCheckoutsContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Checkouts that hold the repo <c language="text">web</c> at <c language="text">/work/web</c> and no other.</summary>
    /// <returns>The checkouts.</returns>
    protected abstract IRepoCheckouts Create();

    /// <summary>A known repo answers its path.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_known_repo_answers_its_checkout()
    {
        var path = await Create().Of(RepoName.From("web"), Token);

        Expect.True(path.IsSuccess);
        Expect.Equal("/work/web", path.Value);
    }

    /// <summary>An unknown repo is an error, not an empty path.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_repo_is_an_error()
    {
        var path = await Create().Of(RepoName.From("other"), Token);

        Expect.True(path.IsError);
    }
}
