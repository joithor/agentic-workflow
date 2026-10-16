using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Lanes;

/// <summary>A running Claude session.</summary>
/// <param name="Session">The session id.</param>
/// <param name="Name">The session name; empty when it has none.</param>
/// <param name="Pid">The process id.</param>
/// <param name="Status">The status the session reports.</param>
/// <param name="Cwd">The session's working directory.</param>
public sealed record LiveSession(SessionId Session, string Name, int Pid, string Status, string Cwd);
