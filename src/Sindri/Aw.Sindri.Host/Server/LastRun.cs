using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Host.Server;

/// <summary>A service's latest recorded run: when its slot was, and how it ended.</summary>
/// <param name="Slot">The slot the run was for (a manual run's is the moment it ran).</param>
/// <param name="Outcome">How it ended.</param>
internal sealed record LastRun(DateTimeOffset Slot, ServiceOutcome Outcome);
