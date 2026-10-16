namespace Aw.Context.Core.Eval.Backtest;

/// <summary>What an eval purge removed (or, for a dry count, would remove).</summary>
/// <param name="Reports">Backtest and map-quality reports.</param>
/// <param name="CacheFiles">As-of map cache files.</param>
/// <param name="ProbeReports">Probe reports (only for 'purge --all', which clears the baselines too).</param>
public sealed record EvalPurgeReport(int Reports, int CacheFiles, int ProbeReports)
{
    /// <summary>All files.</summary>
    public int Total => Reports + CacheFiles + ProbeReports;
}
