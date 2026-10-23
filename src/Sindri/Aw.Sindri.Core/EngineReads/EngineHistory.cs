namespace Aw.Sindri.Core.EngineReads;

/// <summary>'GET /sindri/engine/instances/{id}/history' (API spec §3.8, E-30): a page of events, newest first.</summary>
/// <param name="Events">The page's events, newest first.</param>
/// <param name="Page">The page, from 1.</param>
/// <param name="Total">The events in the history.</param>
/// <param name="Next">The next page, or null on the last.</param>
public sealed record EngineHistory(IReadOnlyList<EngineEvent> Events, int Page, long Total, int? Next)
{
    /// <summary>The events on a page.</summary>
    public const int PageSize = 50;
}
