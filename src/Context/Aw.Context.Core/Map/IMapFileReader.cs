using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>Reads a map file (YAML or JSON) from outside every git worktree.</summary>
public interface IMapFileReader
{
    /// <summary>Reads and parses <paramref name="path"/>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file, SND-CONTEXT-005 inside a worktree, or SND-CONTEXT-031 when it can't be read or parsed.</returns>
    public ValueTask<Result<MapFile>> Read(string path, CancellationToken cancellationToken);
}
