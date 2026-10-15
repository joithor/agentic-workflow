using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a view is built with besides the interpreter's state: the time it is as of, and what only the workflow knows.</summary>
/// <param name="Now">The time ages and timeouts are measured to (the status layout, its header and its timeout line).</param>
/// <param name="Version">The instance's version: the count of events mirrored into the ledger.</param>
/// <param name="Provenance">Who produced each fact and when; a fact with no entry shows no 'by' and 'at'.</param>
/// <param name="Paused">What was left of the state's timeout when a takeover paused it.</param>
/// <param name="Children">The live children (a park or escalation mirroring a child names the first); the group rows of the view wait for engine P7.</param>
public sealed record InstanceViewContext(
    DateTimeOffset Now,
    int Version = 0,
    IReadOnlyDictionary<FactKey, FactProvenance>? Provenance = null,
    TimeSpan? Paused = null,
    IReadOnlyList<ChildView>? Children = null);
