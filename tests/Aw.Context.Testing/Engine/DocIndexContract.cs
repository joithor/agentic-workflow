using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="IDocIndex"/> honours, run against the fake and the SQLite index over the fixture
/// repo (its two docs under 'docs/'). A binding creates the index (and whatever store it needs) and carries its lane.
/// </summary>
/// <typeparam name="TIndex">The implementation.</typeparam>
public abstract class DocIndexContract<TIndex> : IDisposable
    where TIndex : IDocIndex
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    /// <summary>The fixture repo as a profile repo.</summary>
    protected ContextRepo Repo => new(ContextRepoName.From("split-panes"), _repo.Path);

    /// <summary>A fresh index with nothing in it.</summary>
    /// <returns>The index.</returns>
    protected abstract TIndex Create();

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Before any refresh a repo has no head and no sync time.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_repo_never_refreshed_has_no_sync()
    {
        var status = Expect(await Create().Status(Repo, CancellationToken.None));

        Check(status is { Head: null, LastSync: null, Documents: 0 }, "a repo never refreshed has nothing");
    }

    /// <summary>A refresh indexes the docs the globs match; a second one keeps them and adds none.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_refresh_indexes_and_a_second_keeps()
    {
        var index = Create();

        var first = Expect(await index.Refresh(Repo, ["docs/**/*.md"], CancellationToken.None));
        var second = Expect(await index.Refresh(Repo, ["docs/**/*.md"], CancellationToken.None));
        var status = Expect(await index.Status(Repo, CancellationToken.None));

        Check(first is { Added: 2, Code: null } && first.Head is not null, $"the first refresh adds the fixture's 2 docs (added {first.Added.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        Check(second is { Added: 0, Kept: 2 }, "the second refresh keeps them");
        Check(status.LastSync is not null && status.Documents == 2 && status.Chunks >= 2 && status.Code is null, "the status reflects the refresh");
    }

    /// <summary>Deletes the fixture repo.</summary>
    /// <param name="disposing">True from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _repo.Dispose();
        }
    }

    private static T Expect<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"{typeof(TIndex).Name}: {result.Error.Code} {result.Error.Message}");

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TIndex).Name}: {what}");
        }
    }
}
