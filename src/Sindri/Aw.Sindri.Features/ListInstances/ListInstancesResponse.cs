using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.ListInstances;

/// <summary>The instances, by address; for '--needs-me', the two sections instead; and the empty state's words when none matched.</summary>
/// <param name="Instances">Every instance listed, by address.</param>
/// <param name="NeedsYou">The Decisions and Tasks sections, for '--needs-me'.</param>
/// <param name="Empty">'nothing needs you' or 'no instances match' when nothing is listed; otherwise null.</param>
public sealed record ListInstancesResponse(IReadOnlyList<InstanceView> Instances, NeedsYouList? NeedsYou, string? Empty);
