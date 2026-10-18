using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSession;

/// <summary>One session in full (collector spec CA-2): its row, and for a human caller its directory and the last 20 state changes of this Sindri run.</summary>
/// <param name="Session">The row, with the socket path added for a human caller.</param>
/// <param name="Cwd">The agent's working directory (human callers only).</param>
/// <param name="StateHistory">The state changes, oldest first (human callers only).</param>
/// <param name="AsOf">When the last pass ran.</param>
public sealed record SessionView(SessionRow Session, string? Cwd, IReadOnlyList<SessionStateEntry>? StateHistory, DateTimeOffset AsOf);
