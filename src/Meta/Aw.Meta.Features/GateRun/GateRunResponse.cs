using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Health;

namespace Aw.Meta.Features.GateRun;

/// <summary>The GateRun response.</summary>
/// <param name="Gate">The recorded run: head, main, scopes, steps with their log paths, verdict.</param>
/// <param name="Heavy">The heavy reservation computed for the queue as the gate began; null when the team turned it off or it could not be kept.</param>
public sealed record GateRunResponse(GateRecord Gate, HeavySlots? Heavy = null);
