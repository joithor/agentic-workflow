using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A trigger waiting for the interpreter's loop (engine P4 unit (a)): from an Update, with the ticket its decision comes back under, or a delivered event or timer (ticket 0).</summary>
/// <param name="Ticket">The Update's ticket; 0 when no Update waits for the decision.</param>
/// <param name="Trigger">The trigger.</param>
internal sealed record Submission(int Ticket, Trigger Trigger);
