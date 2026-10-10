using Aw.Kernel;
using Aw.Readiness.Core;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Testing;

/// <summary>The in-memory fake of <see cref="IRepoReader"/>: roots map to in-memory trees.</summary>
public sealed class InMemoryRepoReader : IRepoReader
{
    private readonly Dictionary<string, IRepoTree> _roots = new(StringComparer.Ordinal);

    /// <summary>Makes a tree openable at a root.</summary>
    /// <param name="root">The root.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>This reader.</returns>
    public InMemoryRepoReader With(string root, IRepoTree tree)
    {
        _roots[root] = tree;
        return this;
    }

    /// <inheritdoc/>
    public Result<IRepoTree> Open(string root) => _roots.TryGetValue(root, out var tree)
        ? Results.Success(tree)
        : ReadinessErrors.RootNotFound.With($"{root} is not a directory; fix: pass --root with the repo's checkout");
}
