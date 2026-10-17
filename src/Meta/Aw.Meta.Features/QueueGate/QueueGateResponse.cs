using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Health;

namespace Aw.Meta.Features.QueueGate;

/// <summary>The QueueGate response.</summary>
/// <param name="Main">The origin/main sha every gate of the train is pinned to; empty when no pull request was gated.</param>
/// <param name="Entries">Every pull request considered, with how it fared.</param>
/// <param name="Gates">The recorded gates, in train order.</param>
/// <param name="Heavy">The heavy reservation computed as the train began; null when off or not kept.</param>
public sealed record QueueGateResponse(string Main, IReadOnlyList<TrainEntry> Entries, IReadOnlyList<GateRecord> Gates, HeavySlots? Heavy = null);
