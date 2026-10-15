using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>One poll's findings.</summary>
/// <param name="Snapshot">The pull requests, main's sha and the disk flag.</param>
/// <param name="Inbox">How many messages the inbox holds now.</param>
internal sealed record Polled(WatchSnapshot Snapshot, int Inbox);
