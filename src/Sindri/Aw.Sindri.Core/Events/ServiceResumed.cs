using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'service.resumed' payload (API spec §3.4): a human resumed a paused service. Public as it is.</summary>
/// <param name="CommandId">The 'ResumeService' command's id.</param>
/// <param name="Service">The service.</param>
public sealed record ServiceResumed(CommandId CommandId, ServiceName Service);
