using Aw.Sindri.Core.Workflow.Shipped;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// What every <see cref="IEmbeddedRecipes"/> does (setup-once spec §5.3): the files come back relative to <c language="text">recipes/sindri</c>, sorted by path (ordinal) without a duplicate, each a flat
/// <c language="text">*.yaml</c> recipe or an <c language="text">evals/&lt;recipe&gt;/&lt;case&gt;.jsonl</c> case; the set holds what the implementation was given, and an empty set is legal.
/// </summary>
public abstract class EmbeddedRecipesContract
{
    /// <summary>The implementation under test, holding the files named by <see cref="ExpectedPaths"/>.</summary>
    protected abstract IEmbeddedRecipes Subject { get; }

    /// <summary>The paths the subject holds, in any order; empty for the empty set.</summary>
    protected abstract IReadOnlyList<string> ExpectedPaths { get; }

    /// <summary>The subject returns exactly the expected files, sorted by path (ordinal).</summary>
    [Fact, Trait("Lane", "unit")]
    public void The_set_holds_the_expected_paths_in_ordinal_order() =>
        Expect.Sequence([.. ExpectedPaths.Order(StringComparer.Ordinal)], Subject.Files().Select(f => f.RelativePath));

    /// <summary>A path is relative to recipes/sindri: flat yaml, or an eval case under evals/&lt;recipe&gt;/.</summary>
    [Fact, Trait("Lane", "unit")]
    public void Every_path_is_a_flat_recipe_or_an_eval_case_with_no_prefix()
    {
        foreach (var file in Subject.Files())
        {
            var parts = file.RelativePath.Split('/');
            var flat = parts.Length == 1 && file.RelativePath.EndsWith(".yaml", StringComparison.Ordinal);
            var evalCase = parts is ["evals", { Length: > 0 }, { } name] && name.EndsWith(".jsonl", StringComparison.Ordinal);

            Expect.True(flat || evalCase);
            Expect.True(!file.RelativePath.StartsWith("aw-recipes", StringComparison.Ordinal) && !file.RelativePath.StartsWith('/'));
        }
    }

    /// <summary>No path is listed twice, and every file has bytes.</summary>
    [Fact, Trait("Lane", "unit")]
    public void No_path_repeats_and_no_file_is_empty()
    {
        var files = Subject.Files();

        Expect.Equal(files.Count, files.Select(f => f.RelativePath).Distinct(StringComparer.Ordinal).Count());
        Expect.True(files.All(f => !f.Bytes.IsEmpty));
    }

    /// <summary>Asking twice gives the same files.</summary>
    [Fact, Trait("Lane", "unit")]
    public void The_set_is_the_same_each_time() =>
        Expect.Sequence(Subject.Files().Select(f => f.RelativePath), Subject.Files().Select(f => f.RelativePath));
}
