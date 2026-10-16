using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'service.paused' payload (API spec §3.4): a human paused a service; the scheduler runs none of its slots until it resumes. Public as it is.</summary>
/// <param name="CommandId">The 'PauseService' command's id.</param>
/// <param name="Service">The service.</param>
public sealed record ServicePaused(CommandId CommandId, ServiceName Service);
