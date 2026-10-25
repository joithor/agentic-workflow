using System.Text;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An in-memory <see cref="IEmbeddedRecipes"/>: the set a test hands it, sorted by path (ordinal) as the port promises. No files is the empty set a build without embedded recipes has.</summary>
public sealed class FakeEmbeddedRecipes : IEmbeddedRecipes
{
    private readonly List<RecipeSetFile> _files;

    /// <summary>A set of the given files.</summary>
    /// <param name="files">The files, in any order.</param>
    public FakeEmbeddedRecipes(IEnumerable<RecipeSetFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _files = [.. files.OrderBy(f => f.RelativePath, StringComparer.Ordinal)];
    }

    /// <summary>The empty set.</summary>
    public static FakeEmbeddedRecipes Empty => new([]);

    /// <summary>A set of text files.</summary>
    /// <param name="files">The files as (relative path, text).</param>
    /// <returns>The fake.</returns>
    public static FakeEmbeddedRecipes Of(params (string Path, string Text)[] files) =>
        new(files.Select(f => new RecipeSetFile(f.Path, Encoding.UTF8.GetBytes(f.Text))));

    /// <inheritdoc/>
    public IReadOnlyList<RecipeSetFile> Files() => _files;
}
