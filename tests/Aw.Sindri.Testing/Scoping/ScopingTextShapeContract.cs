using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scoping;
using Xunit;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The contract every <see cref="IScopingTextShape"/> honours, run against the in-memory shape and the context engine's adapter.</summary>
public abstract class ScopingTextShapeContract
{
    private static readonly RepoName _repo = RepoName.From("fixture-notes");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The repo the shape answers for.</summary>
    protected virtual RepoName Repo => _repo;

    /// <summary>A shape over a repo that has a map.</summary>
    /// <returns>The shape.</returns>
    protected abstract IScopingTextShape Create();

    /// <summary>A question about a mapped repo is answered, with suggestions that name no anchor twice.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_question_about_a_mapped_repo_is_answered_with_distinct_anchors()
    {
        var anchors = await Create().Anchors(Repo, "links expire after a chosen time", Token);

        Expect.True(anchors.IsSuccess);
        Expect.Equal(anchors.Value.Count, anchors.Value.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>With no repo there is no map to ask.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_question_with_no_repo_is_an_error()
    {
        var anchors = await Create().Anchors(repo: null, "links expire", Token);

        Expect.True(anchors.IsError);
    }

    /// <summary>A question with no words is refused.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_question_with_no_words_is_an_error()
    {
        var anchors = await Create().Anchors(Repo, "   ", Token);

        Expect.True(anchors.IsError);
    }
}
