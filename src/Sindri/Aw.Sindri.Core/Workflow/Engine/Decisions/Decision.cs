using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// What deciding one trigger produced (spec W12–W14): <see cref="Applied"/> and <see cref="Refused"/> are decisions,
/// recorded under the key and replayed; <see cref="Absorbed"/> records the key without changing a row;
/// <see cref="Rejected"/> is a validation refusal that records and consumes nothing.
/// </summary>
/// <param name="Key">The trigger's key.</param>
public abstract record Decision([property: JsonRequired] TriggerKey Key);
