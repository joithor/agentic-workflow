using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSession;

/// <summary>One live session (GET /sindri/sessions/{provider}/{id}; collector spec CA-2).</summary>
/// <param name="Ref">The session's name.</param>
public sealed record GetSessionRequest(SessionRef Ref) : IQuery<SessionView>;
