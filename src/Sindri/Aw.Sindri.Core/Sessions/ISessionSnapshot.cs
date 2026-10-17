namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// The collector's in-memory snapshot (collector spec CO3): live state isn't ledgered, so queries read this. A named exception to
/// "queries read views.db" (docs/csharp/sindri.md). Null until the first good pass.
/// </summary>
public interface ISessionSnapshot
{
    /// <summary>The latest snapshot, or null before the first pass.</summary>
    public SessionSnapshot? Current { get; }

    /// <summary>Replaces the snapshot.</summary>
    /// <param name="snapshot">The new snapshot.</param>
    public void Publish(SessionSnapshot snapshot);
}
