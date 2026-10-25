using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>A suspension that has begun and not yet ended.</summary>
/// <param name="Kind">Its kind.</param>
/// <param name="StartedAt">When it began.</param>
/// <param name="Reason">A park's reason.</param>
internal sealed record StatsOpen(SuspensionKind Kind, DateTimeOffset StartedAt, ParkReason? Reason = null);
