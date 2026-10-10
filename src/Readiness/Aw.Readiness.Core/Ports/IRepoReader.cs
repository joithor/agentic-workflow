using Aw.Kernel;

namespace Aw.Readiness.Core.Ports;

/// <summary>Opens a repo checkout for reading (the file-system port).</summary>
public interface IRepoReader
{
    /// <summary>Opens the checkout at a root directory.</summary>
    /// <param name="root">The repo root.</param>
    /// <returns>The tree, or AW-READINESS-002 when the root is not a readable directory.</returns>
    public Result<IRepoTree> Open(string root);
}
