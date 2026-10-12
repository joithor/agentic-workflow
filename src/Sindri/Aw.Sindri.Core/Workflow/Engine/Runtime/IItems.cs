using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The items the engine starts instances on (port; engine spec rev 8 §6.1 addresses). API P3 (items) owns the store and
/// its adapter; until it lands the engine uses a stub that resolves an item to itself with no aliases.
/// </summary>
public interface IItems
{
    /// <summary>Resolves an ItemId or one of its tracker-key aliases.</summary>
    /// <param name="reference">'ITM-…' or a tracker key ('T-3').</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The item, or an error when no item has that id or alias.</returns>
    public ValueTask<Result<ResolvedItem>> Resolve(string reference, CancellationToken cancellationToken);
}
