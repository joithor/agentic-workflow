using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>
/// Runs the real planner in-process over a map and sources the backtest chooses (S-7A). It is not reachable from the query API or MCP: no as-of, arm or
/// graph parameter is added to them, so no session can ever see an as-of or arm view.
/// </summary>
public interface IArmQuery
{
    /// <summary>Asks the question.</summary>
    /// <param name="question">The question and what it may read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The packed result, or the query's error.</returns>
    public ValueTask<Result<ContextResult>> Ask(ArmQuestion question, CancellationToken cancellationToken);
}
