using Aw.Kernel;

namespace Aw.Context.Core.Eval;

/// <summary>Reads a probe file from outside every git worktree.</summary>
public interface IProbeFileReader
{
    /// <summary>Reads, parses and checks a probe file.</summary>
    /// <param name="path">An absolute path, or null for $AW_SINDRI_HOME/context/eval/probes.json.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file; SND-CONTEXT-005 inside a worktree, 035 when invalid, 036 when it doesn't exist.</returns>
    public ValueTask<Result<ProbeFile>> Read(string? path, CancellationToken cancellationToken);
}
