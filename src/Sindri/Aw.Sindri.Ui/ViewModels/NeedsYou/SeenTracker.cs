namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

#pragma warning disable MA0182 // red stub: the panel uses it in the green commit

/// <summary>Which needs-you rows the user has seen (UI spec §5).</summary>
internal sealed class SeenTracker : ObservableObject
{
    /// <summary>How long a row must be on screen and focused to count as seen.</summary>
    internal static readonly TimeSpan OnScreenFor = TimeSpan.FromSeconds(1);

    /// <summary>How long Mark all seen offers Undo (provisional owner default, 2026-10-14 night).</summary>
    internal static readonly TimeSpan UndoFor = TimeSpan.FromSeconds(10);

    internal SeenTracker(Preferences preferences, TimeProvider clock)
    {
        throw new NotSupportedException();
    }

    /// <summary>The ids seen, newest last.</summary>
    internal IReadOnlyList<string> Seen { get; } = [];

    /// <summary>How many rows the last Mark all seen marked while its Undo is still offered; 0 when none is.</summary>
    internal int UndoCount { get; }

    internal ValueTask Restore(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal bool IsSeen(string id) => throw new NotSupportedException();

    internal void Retain(IReadOnlyCollection<string> current) => throw new NotSupportedException();

    internal void Watch(string id, bool onScreenAndFocused) => throw new NotSupportedException();

    internal void Acted(string id) => throw new NotSupportedException();

    internal void MarkAllSeen(IReadOnlyCollection<string> current) => throw new NotSupportedException();

    internal bool Undo() => throw new NotSupportedException();
}

#pragma warning restore MA0182
