using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>The instance: state, 'next', 'allowedCommands', the open question or desk task, the run token, the children.</summary>
/// <param name="Instance">The view.</param>
public sealed record GetInstanceResponse(InstanceView Instance);
