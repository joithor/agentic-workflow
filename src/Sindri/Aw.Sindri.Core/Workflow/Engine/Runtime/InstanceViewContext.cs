using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a view is built with besides the interpreter's state: what only the workflow knows. It holds no clock; the composers take the time they read at.</summary>
/// <param name="Version">The instance's version: the count of events mirrored into the ledger.</param>
/// <param name="Provenance">Who produced each fact and when; a fact with no entry shows no 'by' and 'at'.</param>
/// <param name="Children">The live children (a park or escalation mirroring a child names the first); the group rows of the view wait for engine P7.</param>
/// <param name="Subject">The subject the workflow named from the first input's stored content; null names it from the recipe alone (the fact key for a text, link or payload).</param>
public sealed record InstanceViewContext(
    int Version = 0,
    IReadOnlyDictionary<FactKey, FactProvenance>? Provenance = null,
    IReadOnlyList<ChildView>? Children = null,
    SubjectRef? Subject = null);
