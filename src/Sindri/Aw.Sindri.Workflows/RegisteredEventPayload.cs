using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What the bridge reads from a registered event's payload (engine spec §2 'EventDescriptor', §5.4): its 'outcome' and
/// the facts it records, by fact key. Other fields are the producer's own and are ignored.
/// </summary>
/// <param name="Outcome">The outcome, one of the descriptor's.</param>
/// <param name="Facts">The recorded facts, by key ('fact:stepSpec').</param>
internal sealed record RegisteredEventPayload(string? Outcome = null, IReadOnlyDictionary<string, FactValue>? Facts = null);
