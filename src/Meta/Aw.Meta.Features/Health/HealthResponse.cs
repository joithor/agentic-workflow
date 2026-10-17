using System.Text.Json.Serialization;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.Health;

/// <summary>What the health check found.</summary>
/// <param name="Groups">Processes by kind with their memory.</param>
/// <param name="ProcessCount">How many processes run.</param>
/// <param name="Host">Memory, swap and disk.</param>
/// <param name="Orphans">The orphans found (Temporal dev servers left behind).</param>
/// <param name="Killed">The pids of the orphans terminated by this run.</param>
/// <param name="Duplicates">MCP server commands running more often than there are sessions to own them.</param>
/// <param name="MissingLanes">Configured lanes with no live session; null when there is no team config to check against.</param>
/// <param name="HeavyLocks">The taken heavy-job lock slots.</param>
/// <param name="MemoryLow">Whether available memory is under a tenth of the total.</param>
/// <param name="Lanes">How each non-Claude lane stands; absent (null) for a team of Claude lanes.</param>
/// <param name="Heavy">The box-wide heavy slots and the meta gates' reservation.</param>
/// <param name="Builders">The builders per lane to run now.</param>
/// <param name="Attention">Whether something needs acting on: an orphan still running, a missing lane or low memory.</param>
public sealed record HealthResponse(
    IReadOnlyList<ProcessGroup> Groups,
    int ProcessCount,
    HostResources Host,
    IReadOnlyList<ProcessInfo> Orphans,
    IReadOnlyList<int> Killed,
    IReadOnlyList<DuplicateServer> Duplicates,
    IReadOnlyList<string>? MissingLanes,
    IReadOnlyList<HeavyLock> HeavyLocks,
    bool MemoryLow,
    bool Attention,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<LaneHealth>? Lanes = null,
    HeavySlots? Heavy = null,
    BuilderAdvice? Builders = null);
