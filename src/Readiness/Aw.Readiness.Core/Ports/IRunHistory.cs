using Aw.Kernel;

namespace Aw.Readiness.Core.Ports;

/// <summary>Keeps the runs of each repo, for the delta and the first-run baseline (spec §8).</summary>
public interface IRunHistory
{
    /// <summary>The repo's most recent run in a scope (of two at the same instant, the one saved last).</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="scope">The scope.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The run, null when there is none yet, or AW-READINESS-004 when the history can't be read.</returns>
    public ValueTask<Result<RunRecord?>> Latest(RepoName repo, string scope, CancellationToken cancellationToken);

    /// <summary>Records a run. A later <see cref="Latest"/> returns it.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Where it was recorded, or AW-READINESS-004.</returns>
    public ValueTask<Result<string>> Save(RunRecord run, CancellationToken cancellationToken);
}
