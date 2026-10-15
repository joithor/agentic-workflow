using Aw.Kernel;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Core.Ports;

/// <summary>Gate runs and merges.</summary>
public interface IGateStore
{
    /// <summary>Stores a gate run and numbers it within its pull request.</summary>
    /// <param name="gate">The run.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stored run with its run number (1 for the first of a pull request).</returns>
    public ValueTask<Result<GateRecord>> Save(GateRecord gate, CancellationToken cancellationToken);

    /// <summary>The latest run for a pull request.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The run, or null when none was recorded.</returns>
    public ValueTask<Result<GateRecord?>> Latest(int pr, CancellationToken cancellationToken);

    /// <summary>Every run for a pull request, newest first, each with its steps.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The runs; empty when none was recorded.</returns>
    public ValueTask<Result<IReadOnlyList<GateRecord>>> History(int pr, CancellationToken cancellationToken);

    /// <summary>Stores a failed step's flake classification.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <param name="run">The run number.</param>
    /// <param name="step">The step name.</param>
    /// <param name="verdict">The classification.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or AW-META-007 when there is no such step.</returns>
    public ValueTask<Result<Unit>> Classify(int pr, int run, string step, FlakeVerdict verdict, CancellationToken cancellationToken);

    /// <summary>Records a merge.</summary>
    /// <param name="merge">The merge.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> RecordMerge(MergeRecord merge, CancellationToken cancellationToken);

    /// <summary>The most recent merge.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The merge, or null when none was recorded.</returns>
    public ValueTask<Result<MergeRecord?>> LatestMerge(CancellationToken cancellationToken);
}
