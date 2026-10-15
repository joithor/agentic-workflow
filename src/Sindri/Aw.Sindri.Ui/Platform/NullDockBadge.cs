namespace Aw.Sindri.Ui.Platform;

/// <summary>The dock badge of a platform that has none: it remembers the count and draws nothing.</summary>
public sealed class NullDockBadge : IDockBadge
{
    /// <inheritdoc />
    public int Shown { get; private set; }

    /// <inheritdoc />
    public void Show(int count) => Shown = Math.Max(count, 0);
}
