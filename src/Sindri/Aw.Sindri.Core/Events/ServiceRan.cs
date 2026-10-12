using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'service.ran' payload (API spec C8): one per scheduled run, whatever its outcome.</summary>
/// <param name="CommandId">The run's command id, minted by its caller context.</param>
/// <param name="Service">The service.</param>
/// <param name="Slot">The scheduled slot it filled.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
public sealed record ServiceRan(CommandId CommandId, ServiceName Service, DateTimeOffset Slot, ServiceOutcome Outcome, string? Code);
