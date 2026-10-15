using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

namespace Aw.Sindri.Features.ListInstances;

/// <summary>The instances, by address; for '--needs-me', the two sections instead; and the empty state's words when none matched.</summary>
/// <param name="Instances">Every instance listed, by address.</param>
/// <param name="NeedsYou">The Decisions and Tasks sections, for '--needs-me'.</param>
/// <param name="Empty">'nothing needs you' or 'no instances match' when nothing is listed; otherwise null.</param>
public sealed record ListInstancesResponse(IReadOnlyList<InstanceView> Instances, NeedsYouList? NeedsYou, string? Empty)
{
    /// <summary>One list row per listed instance, in the same order as <see cref="Instances"/> (UI screens E-20, API spec §3.8 'InstanceList.instances'), each built by <see cref="InstanceSummaries.Of"/>.</summary>
    public IReadOnlyList<InstanceSummary> Rows { get; init; } = [];
}
