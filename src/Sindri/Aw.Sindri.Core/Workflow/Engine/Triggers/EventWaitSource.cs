using System.Globalization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>An event the router delivered through the inbox (spec §5.7); its id is the source ledger sequence.</summary>
/// <param name="Kind">The event's kind.</param>
/// <param name="Sequence">Its ledger sequence.</param>
public sealed record EventWaitSource(EventKind Kind, long Sequence) : WaitSource
{
    /// <inheritdoc/>
    public override string KeyPart => string.Create(CultureInfo.InvariantCulture, $"event:{Sequence}");
}
