namespace Aw.Context.Core.Graph;

/// <summary>One step of the key function: what a key of version <see cref="From"/> becomes at <see cref="To"/> (spec §6.1).</summary>
public interface IKeyMigration
{
    /// <summary>The key version it reads.</summary>
    public int From { get; }

    /// <summary>The key version it writes (From + 1).</summary>
    public int To { get; }

    /// <summary>The new key for an old one.</summary>
    /// <param name="key">A key of version <see cref="From"/>.</param>
    /// <returns>Its version-<see cref="To"/> key (the same key when nothing changes).</returns>
    public NodeKey Migrate(NodeKey key);
}
