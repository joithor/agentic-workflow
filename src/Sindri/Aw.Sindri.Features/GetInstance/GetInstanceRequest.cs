using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>'workflow status &lt;address&gt;' (engine spec §6.3, §6.4): the instance as its 'GetInstance' Query shows it.</summary>
/// <param name="Address">The instance.</param>
public sealed record GetInstanceRequest(InstanceAddress Address) : IQuery<GetInstanceResponse>;
