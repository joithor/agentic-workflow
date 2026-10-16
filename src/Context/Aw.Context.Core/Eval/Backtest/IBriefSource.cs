using Aw.Kernel;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Supplies the issues a backtest could sample and their briefs (S-7A: through the Onyx adapter's allowlist; the native Linear source after S-6).</summary>
public interface IBriefSource
{
    /// <summary>The issues of the profile's Linear teams the source can list: refs, versions and dates only.</summary>
    /// <param name="scope">The call's scope.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The issues in key order, SND-CONTEXT-081 when no configured source can supply them, or SND-CONTEXT-011 when the source is down.</returns>
    public ValueTask<Result<IReadOnlyList<BriefIssue>>> Issues(BriefScope scope, CancellationToken cancellationToken);

    /// <summary>One issue's brief.</summary>
    /// <param name="scope">The call's scope.</param>
    /// <param name="issue">The issue.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The brief; null when the source no longer has the record (the sample is dropped, not an error); or an error when the source is down.</returns>
    public ValueTask<Result<Brief?>> Brief(BriefScope scope, BriefIssue issue, CancellationToken cancellationToken);
}
