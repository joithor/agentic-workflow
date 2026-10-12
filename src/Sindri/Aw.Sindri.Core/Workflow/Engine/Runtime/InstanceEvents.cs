using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Which ledger events belong to an instance (engine spec §5.6, §6.3 'workflow events'; S1-D6): the interpreter
/// mirrors each under the key 'workflow:{workflow id}:{run id}:{activity id}', and the workflow id is the address, so
/// an instance's events are the ones keyed under its address. A child's address extends its parent's with '/', so the
/// ':' after the address keeps a parent's filter from taking its children's events.
/// </summary>
public static class InstanceEvents
{
    /// <summary>The empty state of 'workflow events'.</summary>
    public const string None = "no events";

    /// <summary>Whether <paramref name="recorded"/> was mirrored by <paramref name="address"/>.</summary>
    /// <param name="recorded">The ledger event.</param>
    /// <param name="address">The instance.</param>
    /// <returns>True when it is one of the instance's events.</returns>
    public static bool Belongs(LedgerEvent recorded, InstanceAddress address)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        return recorded.IdempotencyKey.StartsWith($"workflow:{address.Value}:", StringComparison.Ordinal);
    }
}
