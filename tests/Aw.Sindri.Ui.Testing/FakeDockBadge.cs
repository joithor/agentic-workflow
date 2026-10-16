using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory dock badge: remembers every count it was given.</summary>
internal sealed class FakeDockBadge : IDockBadge
{
    private readonly List<int> _history = [];

    public int Shown { get; private set; }

    /// <summary>Every count shown, in order.</summary>
    public IReadOnlyList<int> History => _history;

    public void Show(int count)
    {
        Shown = Math.Max(count, 0);
        _history.Add(Shown);
    }
}
