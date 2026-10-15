using Aw.Meta.Core.Health;

namespace Aw.Meta.Features.Health;

/// <summary>Everything read from the host in one go.</summary>
/// <param name="Snapshot">The processes.</param>
/// <param name="Resources">Memory, swap and disk.</param>
/// <param name="Locks">The taken heavy-job lock slots.</param>
internal sealed record HostReading(HostSnapshot Snapshot, HostResources Resources, IReadOnlyList<HeavyLock> Locks);
