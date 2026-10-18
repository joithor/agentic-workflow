namespace Aw.Sindri.Features.ListSessions;

/// <summary>The counts a page head shows, over the sessions the caller may see (before the request's filters).</summary>
/// <param name="Live">Every live session.</param>
/// <param name="Asking">The ones waiting on the human.</param>
/// <param name="OutsideWorkflow">The ones with no Sindri launch.</param>
public sealed record SessionCounts(int Live, int Asking, int OutsideWorkflow);
