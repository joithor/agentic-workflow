using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>
/// 'workflow status &lt;instance&gt;' (engine spec §6.3, §6.4) and 'GET /workflow/instances/{instanceId}': the instance as its
/// 'GetInstance' Query shows it.
/// </summary>
/// <param name="Instance">The instance: its id ('WFI-…', what the route carries), its address, or a bare item for the root of its latest attempt.</param>
public sealed record GetInstanceRequest(string Instance) : IQuery<InstanceDetail>;
