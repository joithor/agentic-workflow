using Aw.Meta.Core.Gate;

namespace Aw.Meta.Features.GateRun;

/// <summary>The GateRun response.</summary>
/// <param name="Gate">The recorded run: head, main, scopes, steps with their log paths, verdict.</param>
public sealed record GateRunResponse(GateRecord Gate);
