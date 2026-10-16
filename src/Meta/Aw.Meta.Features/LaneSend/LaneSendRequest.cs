namespace Aw.Meta.Features.LaneSend;

/// <summary>The LaneSend request: the meta sends a message to a non-Claude lane.</summary>
/// <param name="Lane">The lane, as named in the team config.</param>
/// <param name="Text">The message text.</param>
public sealed record LaneSendRequest(string Lane, string Text);
