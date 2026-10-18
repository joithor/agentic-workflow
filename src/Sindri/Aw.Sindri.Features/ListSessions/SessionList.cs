using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.ListSessions;

/// <summary>The live sessions (collector spec CA-1).</summary>
/// <param name="Sessions">The sessions the caller may see, after the filters, by ref.</param>
/// <param name="RecentlyEnded">The ones that ended in the last ten minutes, newest first.</param>
/// <param name="Problems">One row per source that failed in the last pass.</param>
/// <param name="Counts">The head counts.</param>
/// <param name="Empty">'No agent sessions are running.' when there is none to show, else null.</param>
/// <param name="AsOf">When the last pass ran.</param>
public sealed record SessionList(
    IReadOnlyList<SessionRow> Sessions, IReadOnlyList<EndedSessionRow> RecentlyEnded, IReadOnlyList<SessionProblem> Problems, SessionCounts Counts, string? Empty, DateTimeOffset AsOf);
