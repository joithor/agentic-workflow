using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>An '--accept' override (spec §5.2, §3.8): only that gate; the others and the evidence still ran.</summary>
/// <param name="Gate">The overridden gate.</param>
public sealed record OverrideRecord(BlockId Gate);
