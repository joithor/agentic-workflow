using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// What deciding one trigger produced (spec §5.1, §5.2): <see cref="Applied"/> and <see cref="Refused"/> change the
/// instance and write its ledger events; <see cref="Absorbed"/> takes the trigger without changing it;
/// <see cref="Rejected"/> is a validation refusal that records and consumes nothing.
/// </summary>
/// <param name="Key">The trigger's key, the id its ledger events carry.</param>
public abstract record Decision([property: JsonRequired] TriggerKey Key);
