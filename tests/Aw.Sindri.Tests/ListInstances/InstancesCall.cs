using Aw.Sindri.Core.Workflow.Engine.Desk;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>One call the list made to the instance port.</summary>
internal sealed record InstancesCall(InstanceFilter? Filter, string? Query, bool Ended);
