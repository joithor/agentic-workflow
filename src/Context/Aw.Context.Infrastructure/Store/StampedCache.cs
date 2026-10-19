using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Store;

/// <summary>
/// What a store loaded, kept by the change stamp (migration 0008) read in the same read transaction: while no write moved the stamp, the same
/// load is answered again without reading the rows (a 24k-node snapshot took a second to read on every map read). A store not yet migrated has no
/// stamp, and then nothing is kept.
/// </summary>
/// <typeparam name="T">What is kept.</typeparam>
internal sealed class StampedCache<T>
    where T : class
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (long Stamp, T Value)> _held = new(StringComparer.Ordinal);

    /// <summary>The change stamp as of <paramref name="session"/>'s read transaction, or null when the store has none yet.</summary>
    internal static async ValueTask<long?> Stamp(SqliteReadSession session, CancellationToken cancellationToken)
    {
        var stamp = await SqliteQueries.One(session, new SqlStatement("SELECT seq FROM change_stamp WHERE id = 1"), Graph.GraphRows.Id, cancellationToken).ConfigureAwait(false);
        return stamp.IsSuccess ? stamp.Value : null;
    }

    /// <summary>What was kept under <paramref name="key"/> at <paramref name="stamp"/>, or null.</summary>
    internal T? Get(string key, long? stamp)
    {
        lock (_gate)
        {
            return stamp is { } now && _held.TryGetValue(key, out var held) && held.Stamp == now ? held.Value : null;
        }
    }

    /// <summary>Keeps <paramref name="value"/> under <paramref name="key"/> at <paramref name="stamp"/> (nothing without a stamp).</summary>
    internal void Put(string key, long? stamp, T? value)
    {
        if (stamp is not { } now || value is null)
        {
            return;
        }

        lock (_gate)
        {
            _held[key] = (now, value);
        }
    }
}
