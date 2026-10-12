using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Views;

/// <summary>A service's last run.</summary>
/// <param name="Slot">The slot it filled.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Code">The error code when it failed.</param>
/// <param name="RanAt">When it was recorded.</param>
public sealed record LastRun(DateTimeOffset Slot, ServiceOutcome Outcome, string? Code, DateTimeOffset RanAt);
