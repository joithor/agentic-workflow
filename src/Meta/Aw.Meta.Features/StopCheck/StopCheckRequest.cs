using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.StopCheck;

/// <summary>The StopCheck request: may this session stop?</summary>
/// <param name="Session">The session that is about to stop.</param>
/// <param name="Transcript">Its transcript, read for new peer messages.</param>
/// <param name="HookActive">Whether the stop hook is already continuing a block (Claude Code's stop_hook_active).</param>
/// <param name="MaxBlocks">How many identical blocks in a row are allowed before a continuation is let through.</param>
public sealed record StopCheckRequest(SessionId Session, string Transcript, bool HookActive, int MaxBlocks);
