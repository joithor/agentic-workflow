using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;
using Aw.Kernel;

namespace Aw.Context.Tests.RunBacktest;

/// <summary>A brief source whose briefs for chosen issues come back as "source down" (SND-CONTEXT-011), the rest read through the real one.</summary>
internal sealed class FlakyBriefSource(IBriefSource inner) : IBriefSource
{
    /// <summary>The issue keys whose brief read fails.</summary>
    public HashSet<string> Down { get; } = new(StringComparer.Ordinal);

    public ValueTask<Result<IReadOnlyList<BriefIssue>>> Issues(BriefScope scope, CancellationToken cancellationToken) => inner.Issues(scope, cancellationToken);

    public async ValueTask<Result<Brief?>> Brief(BriefScope scope, BriefIssue issue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return Down.Contains(issue.Key) ? ContextErrors.AllSourcesUnavailable.With("the brief source is down") : await inner.Brief(scope, issue, cancellationToken).ConfigureAwait(false);
    }
}
