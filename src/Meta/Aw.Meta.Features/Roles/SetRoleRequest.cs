using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.Roles;

/// <summary>The SetRole request: register a session as the meta.</summary>
/// <param name="Session">The session id.</param>
/// <param name="Name">The session name; empty when it has none.</param>
public sealed record SetRoleRequest(SessionId Session, string Name);
