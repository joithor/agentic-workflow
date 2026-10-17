using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;
using Aw.Kernel;

namespace Aw.Context.Tests.RunBacktest;

/// <summary>A brief source whose briefs for chosen issues come back as "source down" (SND-CONTEXT-011), the rest read through the real one.</summary>
internal sealed class FlakyBriefSource(IBriefSource inner) : IBriefSource
{
    /// <summary>The issue keys whose brief read fails.</summary>
    public HashSet<string> Down { get; } = new(StringComparer.Ordinal);

    /// <summary>Teams to report as capped, whatever the inner listing says.</summary>
    public List<string> Capped { get; } = [];

    public async ValueTask<Result<BriefListing>> Issues(BriefScope scope, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        var listing = await inner.Issues(scope, since, cancellationToken).ConfigureAwait(false);
        return listing.IsError ? listing.Error : listing.Value with { CappedTeams = [.. listing.Value.CappedTeams, .. Capped] };
    }

    public async ValueTask<Result<Brief?>> Brief(BriefScope scope, BriefIssue issue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return Down.Contains(issue.Key) ? ContextErrors.AllSourcesUnavailable.With("the brief source is down") : await inner.Brief(scope, issue, cancellationToken).ConfigureAwait(false);
    }
}
