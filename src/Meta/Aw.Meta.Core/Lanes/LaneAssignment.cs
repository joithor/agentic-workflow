using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Lanes;

/// <summary>A lane and the session working it.</summary>
/// <param name="Lane">The lane name.</param>
/// <param name="SessionName">The session's name, which is how messages address it.</param>
/// <param name="Session">The session id when known.</param>
/// <param name="Source">How the match was made.</param>
public sealed record LaneAssignment(string Lane, string SessionName, SessionId? Session, LaneSource Source);
