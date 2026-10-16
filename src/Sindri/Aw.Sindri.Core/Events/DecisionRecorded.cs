using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'decision.recorded' payload (API spec §3.4): a human chose an option. Public as it is.</summary>
/// <param name="CommandId">The 'Decide' command's id.</param>
/// <param name="Decision">The decision.</param>
/// <param name="Choice">The option chosen.</param>
/// <param name="Note">The note given with it (the projection scrubs it), or null.</param>
/// <param name="By">Who decided, as the envelope's actor reads.</param>
public sealed record DecisionRecorded(CommandId CommandId, DecisionId Decision, DecisionOption Choice, string? Note, string By);
