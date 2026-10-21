namespace Aw.Sindri.Core.Setup;

/// <summary>The per-repo lines of a setup step, with value equality so a fact that carries them compares by what they say.</summary>
/// <param name="Lines">The lines, in the profile's repo order.</param>
public sealed record SetupItems(IReadOnlyList<SetupItem> Lines)
{
    /// <summary>No lines.</summary>
    public static SetupItems None { get; } = new([]);

    /// <inheritdoc/>
    public bool Equals(SetupItems? other) => other is not null && Lines.SequenceEqual(other.Lines);

    /// <inheritdoc/>
    public override int GetHashCode() => Lines.Count;
}
