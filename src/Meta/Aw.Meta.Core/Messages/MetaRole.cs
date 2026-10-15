using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Messages;

/// <summary>The session registered as the meta; hooks act only for it.</summary>
/// <param name="Session">The session id.</param>
/// <param name="Name">The session name; empty when it has none.</param>
public sealed record MetaRole(SessionId Session, string Name);
